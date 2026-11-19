using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Nara.Practice
{
    public sealed class PracticeRig : MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> displays = new();
        readonly List<XRInputSubsystem> inputs = new();
        NaraPractice app;
        PracticeView view;
        bool lastA, lastB, lastY, lastRightGrip, lastLeftGrip, lastSkip;
        float yaw, pitch, holdToEnd, framingYaw, framingVelocity;
        public bool Active { get; private set; }

        public void Initialise(NaraPractice practice)
        {
            app = practice;
            view = GetComponent<PracticeView>();
        }

        void OnEnable() => Application.onBeforeRender += ApplyPose;
        void OnDisable() => Application.onBeforeRender -= ApplyPose;

        void Update()
        {
            bool playingXr = false;
            SubsystemManager.GetSubsystems(displays);
            foreach (XRDisplaySubsystem display in displays)
                playingXr |= display.running;
            if (playingXr != Active)
            {
                Active = playingXr;
                if (Active)
                {
                    SubsystemManager.GetSubsystems(inputs);
                    foreach (XRInputSubsystem input in inputs)
                    {
                        if (input.running)
                            input.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                    }
                }
                view.SetXr(Active);
                app.ApplyPresentationMode();
            }
            if (Active)
                ReadControllers();
            else
                ReadDesktop();
        }

        void ReadDesktop()
        {
            PracticePhase phase = app.Session.Phase;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                view.MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && phase != PracticePhase.Setup && !view.Editing)
            {
                app.ResetPractice();
                return;
            }
            if (Input.GetKeyUp(KeyCode.Space))
                app.EndTalk();
            if (Input.GetKeyDown(KeyCode.F12))
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "Nara-view.png"));
            if (view.Editing)
                return;
            if (phase == PracticePhase.Setup && (Input.GetKeyDown(KeyCode.C) ||
                (view.PickingAvatars && Input.GetKeyDown(KeyCode.Escape))))
                view.ToggleAvatarPicker();
            else if (phase == PracticePhase.Setup && Input.GetKeyDown(KeyCode.Escape))
                view.CloseDesktopSection();
            bool picking = view.PickingAvatars;
            if (!picking && phase == PracticePhase.Setup && Input.GetKeyDown(KeyCode.B))
                view.SelectBaseline();
            GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            Selectable control = selected ? selected.GetComponent<Selectable>() : null;
            if (!picking && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                (!control || !control.IsActive() || !control.IsInteractable()))
                PrimaryAction();
            if (Input.GetKeyDown(KeyCode.P))
                app.TogglePause();
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (picking) view.StepAvatar(1);
                else app.ChangeSlide(1);
            }
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (picking) view.StepAvatar(-1);
                else app.ChangeSlide(-1);
            }
            if (Input.GetKeyDown(KeyCode.Space))
                app.BeginTalk();

            if (!app.Baseline && Input.GetMouseButton(1) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * 1.6f + 180, 360) - 180;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.4f, -32, 48);
            }
            if (Input.GetKeyDown(KeyCode.Home))
                yaw = pitch = 0;
            framingYaw = Mathf.SmoothDamp(framingYaw, phase == PracticePhase.Setup ? -11f : 0f,
                ref framingVelocity, .18f, Mathf.Infinity, Time.unscaledDeltaTime);
            Transform eyes = app.practiceCamera.transform;
            eyes.localPosition = Vector3.zero;
            if (!app.Baseline && phase is not (PracticePhase.Setup or PracticePhase.Review) &&
                app.confidenceScreen && Input.GetKey(KeyCode.V))
                eyes.rotation = Quaternion.LookRotation(app.confidenceScreen.transform.position - eyes.position);
            else
                eyes.localRotation = Quaternion.Euler(app.desktopPitch + pitch, yaw + framingYaw, 0);
        }

        void ReadControllers()
        {
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool a = Read(right, CommonUsages.primaryButton);
            bool b = Read(right, CommonUsages.secondaryButton);
            bool x = Read(left, CommonUsages.primaryButton);
            bool y = Read(left, CommonUsages.secondaryButton);
            bool rightGrip = Read(right, CommonUsages.gripButton);
            bool leftGrip = Read(left, CommonUsages.gripButton);
            bool skip = Read(left, CommonUsages.triggerButton);

            bool setup = app.Session.Phase == PracticePhase.Setup;
            if (setup && leftGrip && !lastLeftGrip)
                view.ToggleAvatarPicker();
            bool picking = view.PickingAvatars;
            if (a && !lastA)
            {
                if (picking) view.NextAvatarSeat();
                else PrimaryAction();
            }
            if (b && !lastB)
            {
                if (picking)
                    view.StepAvatar(1);
                else if (setup)
                    view.ToggleCloud();
                else
                    app.ChangeSlide(1);
            }
            if (y && !lastY)
            {
                if (picking)
                    view.StepAvatar(-1);
                else if (setup)
                    view.ToggleLanguage();
                else
                    app.ChangeSlide(-1);
            }
            if (!setup && leftGrip && !lastLeftGrip)
            {
                if (app.Session.Phase == PracticePhase.Presentation)
                    app.TogglePause();
                else
                    app.Replay();
            }
            if (rightGrip && !lastRightGrip)
                app.BeginTalk();
            if (!rightGrip && lastRightGrip)
                app.EndTalk();
            if (!picking && skip && !lastSkip)
            {
                if (app.Session.Phase == PracticePhase.Setup)
                    view.ToggleMetrics();
                else
                    app.Answer("", true);
            }
            if (x && app.Session.Phase != PracticePhase.Setup)
            {
                holdToEnd += Time.unscaledDeltaTime;
                if (holdToEnd >= 1f)
                    app.ResetPractice();
            }
            else
            {
                holdToEnd = 0;
            }
            lastA = a;
            lastB = b;
            lastY = y;
            lastRightGrip = rightGrip;
            lastLeftGrip = leftGrip;
            lastSkip = skip;
        }

        void PrimaryAction()
        {
            switch (app.Session.Phase)
            {
                case PracticePhase.Setup: view.Begin(); break;
                case PracticePhase.Presentation: app.FinishPresentation(); break;
                case PracticePhase.FloorRequest: app.GrantFloor(); break;
                case PracticePhase.Question:
                    if (!Active || !app.Live)
                        view.Submit();
                    break;
                case PracticePhase.Review: app.ResetPractice(); break;
            }
        }

        void ApplyPose()
        {
            if (!Active || app == null)
                return;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
                app.practiceCamera.transform.localPosition = position;
            if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
                app.practiceCamera.transform.localRotation = rotation;
        }

        static bool Read(InputDevice device, InputFeatureUsage<bool> feature) =>
            device.TryGetFeatureValue(feature, out bool pressed) && pressed;
    }
}
