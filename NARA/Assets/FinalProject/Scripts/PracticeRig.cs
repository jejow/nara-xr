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
        bool lastA, lastB, lastX, lastY, lastRightGrip, lastLeftGrip, lastSkip, lastType;
        bool classroomGripReady;
        float yaw, pitch, holdToEnd, framingYaw, framingVelocity;
        public bool Active { get; private set; }
        internal bool ViewingProjection { get; private set; }

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
                    ViewingProjection = false;
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
            else if (!NaraQuestBridge.IsStandalone)
                ReadDesktop();
        }

        void ReadDesktop()
        {
            PracticePhase phase = app.Session.Phase;
            if (phase is not (PracticePhase.Setup or PracticePhase.Presentation))
                ViewingProjection = false;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                view.MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && phase != PracticePhase.Setup && !view.Editing)
            {
                if (ViewingProjection)
                    ToggleProjection();
                else
                    app.ResetPractice();
                return;
            }
            if (Input.GetKeyUp(KeyCode.Space))
                app.EndTalk();
            if (Input.GetKeyDown(KeyCode.F12))
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath, "Nara-view.png"));
            if (view.Editing)
                return;
            if (phase == PracticePhase.Setup && Input.GetKeyDown(KeyCode.Escape))
            {
                if (ViewingProjection)
                    ToggleProjection();
                else
                    view.CloseDesktopSection();
            }
            if (app.IsIf101 && Input.GetKeyDown(KeyCode.V))
                ToggleProjection();
            if (phase == PracticePhase.Setup && Input.GetKeyDown(KeyCode.B))
                view.SelectBaseline();
            GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            Selectable control = selected ? selected.GetComponent<Selectable>() : null;
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                (!control || !control.IsActive() || !control.IsInteractable()))
                PrimaryAction();
            if (Input.GetKeyDown(KeyCode.P))
                app.TogglePause();
            if (Input.GetKeyDown(KeyCode.RightArrow) && !view.TurnQuestionPage(1))
                app.ChangeSlide(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) && !view.TurnQuestionPage(-1))
                app.ChangeSlide(-1);
            if (Input.GetKeyDown(KeyCode.Space))
                app.BeginTalk();

            if (!app.Baseline && !ViewingProjection && Input.GetMouseButton(1) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * 1.6f + 180, 360) - 180;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.4f, -32, 48);
            }
            if (Input.GetKeyDown(KeyCode.Home))
                yaw = pitch = 0;
            framingYaw = Mathf.SmoothDamp(framingYaw, phase == PracticePhase.Setup ? -11f : 0f,
                ref framingVelocity, .18f, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyDesktopView();
        }

        internal void ToggleProjection()
        {
            if (Active || !app.IsIf101 || app.Baseline || !app.Slide ||
                app.Session.Phase is not (PracticePhase.Setup or PracticePhase.Presentation))
                return;
            ViewingProjection = !ViewingProjection;
            ApplyDesktopView();
        }

        void ApplyDesktopView()
        {
            Transform eyes = app.practiceCamera.transform;
            eyes.localPosition = Vector3.zero;
            PracticePhase phase = app.Session.Phase;
            if (!app.Baseline && ViewingProjection)
            {
                Transform screen = app.slideScreen.transform;
                eyes.position = screen.position - screen.forward * 2.78f - Vector3.up * .33f;
                eyes.rotation = Quaternion.LookRotation(screen.position - eyes.position);
            }
            else if (!app.Baseline && phase is not (PracticePhase.Setup or PracticePhase.Review) &&
                app.confidenceScreen && Input.GetKey(KeyCode.V))
            {
                Transform display = app.confidenceScreen.transform;
                eyes.rotation = Quaternion.LookRotation(display.position - eyes.position);
            }
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
            bool type = Read(right, CommonUsages.triggerButton);

            bool setup = app.Session.Phase == PracticePhase.Setup;
            bool gated = view.UpdateQuestInputGate(a || b || x || y || rightGrip || leftGrip || skip || type);
            if (!gated && setup && NaraQuestBridge.IsStandalone && (view.QuestSettingsOpen || rightGrip && !lastRightGrip))
            {
                view.QuestSetupControls(a && !lastA, b && !lastB, x && !lastX, y && !lastY,
                    rightGrip && !lastRightGrip, leftGrip && !lastLeftGrip, type && !lastType);
            }
            else if (!gated)
            {
                ReadPracticeControls(setup, a, b, x, y, rightGrip, leftGrip, skip, type);
            }
            lastA = a;
            lastB = b;
            lastX = x;
            lastY = y;
            lastRightGrip = rightGrip;
            lastLeftGrip = leftGrip;
            lastSkip = skip;
            lastType = type;
        }

        void ReadPracticeControls(bool setup, bool a, bool b, bool x, bool y,
            bool rightGrip, bool leftGrip, bool skip, bool type)
        {
            if (type && !lastType && NaraQuestBridge.IsStandalone && !app.Recording)
                view.OpenQuestResponseKeyboard();
            else if (a && !lastA)
                PrimaryAction();
            if (b && !lastB)
            {
                if (setup)
                    view.ToggleCloud();
                else if (!view.TurnQuestionPage(1))
                    app.ChangeSlide(1);
            }
            if (y && !lastY)
            {
                if (setup)
                    view.ToggleLanguage();
                else if (!view.TurnQuestionPage(-1))
                    app.ChangeSlide(-1);
            }
            if (!leftGrip)
                classroomGripReady = true;
            if (leftGrip && !lastLeftGrip)
            {
                if (setup)
                {
                    if (classroomGripReady)
                        view.ToggleClassroom();
                }
                else if (app.Session.Phase == PracticePhase.Presentation)
                    app.TogglePause();
                else
                    app.Replay();
            }
            if (rightGrip && !lastRightGrip)
                app.BeginTalk();
            if (!rightGrip && lastRightGrip)
                app.EndTalk();
            if (skip && !lastSkip)
            {
                if (app.Session.Phase == PracticePhase.Setup)
                    view.ToggleMetrics();
                else
                    app.Answer("", true);
            }
            if (x && !setup)
            {
                holdToEnd += Time.unscaledDeltaTime;
                if (holdToEnd >= 1f)
                    app.ResetPractice();
            }
            else
            {
                holdToEnd = 0;
            }
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
            view.Refresh();
        }

        static bool Read(InputDevice device, InputFeatureUsage<bool> feature) =>
            device.TryGetFeatureValue(feature, out bool pressed) && pressed;
    }
}
