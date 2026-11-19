using System;
using System.Collections.Generic;

namespace Nara.Practice
{
    public enum PracticePhase { Setup, Presentation, Waiting, FloorRequest, Asking, Question, Review }

    [Serializable]
    public sealed class Exchange
    {
        public string question;
        public string answer;
    }

    [Serializable]
    public sealed class TurnRequest
    {
        public string operation;
        public string language;
        public string speaker;
        public string lead;
        public string topic;
        public string summary;
        public string presentation;
        public string question;
        public string answer;
        public string audio;
        public Exchange[] history;
    }

    [Serializable]
    public sealed class TurnReply
    {
        public string kind;
        public string text;
        public string transcript;
        public string anchor;
        public string audio;
        public string source;
        public string speaker;
    }

    public sealed class PracticeSession
    {
        public const int Duration = 180;
        public const string FirstQuestion = "question_1";
        public const string SecondQuestion = "question_2";
        public const string AfterSecond = "after_second";
        public const string Feedback = "feedback";
        public const string FollowUp = "follow_up";

        readonly List<Exchange> history = new(3);

        public PracticePhase Phase { get; private set; }
        public string Topic { get; private set; }
        public string Summary { get; private set; }
        public string Presentation { get; set; }
        public string Operation { get; private set; }
        public string Question { get; private set; }
        public string Suggestion { get; private set; }
        public bool Paused { get; private set; }
        public bool FollowUpAsked { get; private set; }
        public bool IsFollowUp { get; private set; }
        public int MainQuestions { get; private set; }
        public int Attempts { get; private set; }
        public int Skips { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Math.Max(0, Duration - Elapsed);

        public void Start(string topic, string summary)
        {
            Reset();
            Topic = topic.Trim();
            Summary = summary.Trim();
            Phase = PracticePhase.Presentation;
        }

        public void Tick(float delta)
        {
            if (Phase == PracticePhase.Presentation && !Paused)
                Elapsed = Math.Min(Duration, Elapsed + delta);
        }

        public void TogglePause()
        {
            if (Phase == PracticePhase.Presentation)
                Paused = !Paused;
        }

        public void BeginQuestions()
        {
            if (Phase == PracticePhase.Presentation)
                WaitFor(FirstQuestion);
        }

        public void RecordAnswer(string answer, bool skipped)
        {
            if (Phase != PracticePhase.Question)
                return;
            history.Add(new Exchange { question = Question, answer = answer ?? "" });
            if (skipped)
                Skips++;
            else
                Attempts++;
            if (MainQuestions == 1)
                WaitFor(SecondQuestion);
            else
                WaitFor(IsFollowUp || skipped ? Feedback : AfterSecond);
        }

        public void SetLatestTranscript(string transcript)
        {
            string text = (transcript ?? "").Trim();
            if (text.Length == 0)
                return;
            if (Operation == FirstQuestion)
                Presentation = text;
            else if (history.Count > 0)
                history[history.Count - 1].answer = text;
        }

        public bool RetryAnswer()
        {
            if (Phase != PracticePhase.Waiting || Operation == FirstQuestion || history.Count == 0)
                return false;
            history.RemoveAt(history.Count - 1);
            Attempts--;
            Phase = PracticePhase.Question;
            Operation = "";
            return true;
        }

        public bool ApplyReply(string kind, string text)
        {
            if (Phase != PracticePhase.Waiting || string.IsNullOrWhiteSpace(text))
                return false;
            if (Operation == FirstQuestion || Operation == SecondQuestion)
            {
                if (kind != "question")
                    return false;
                IsFollowUp = false;
            }
            else if (kind == FollowUp && Operation == AfterSecond && !FollowUpAsked)
            {
                IsFollowUp = true;
            }
            else if (kind == Feedback && (Operation == Feedback || Operation == AfterSecond))
            {
                Suggestion = text.Trim();
                Phase = PracticePhase.Review;
                Operation = "";
                return true;
            }
            else
            {
                return false;
            }
            Question = text.Trim();
            Phase = PracticePhase.FloorRequest;
            Operation = "";
            return true;
        }

        public bool GrantFloor()
        {
            if (Phase != PracticePhase.FloorRequest)
                return false;
            if (IsFollowUp)
                FollowUpAsked = true;
            else
                MainQuestions++;
            Phase = PracticePhase.Asking;
            return true;
        }

        public void OpenAnswer()
        {
            if (Phase == PracticePhase.Asking)
                Phase = PracticePhase.Question;
        }

        public bool RepeatQuestion()
        {
            if (Phase != PracticePhase.Question)
                return false;
            Phase = PracticePhase.Asking;
            return true;
        }

        public TurnRequest Request(string language, byte[] audio)
        {
            Exchange latest = history.Count > 0 ? history[history.Count - 1] : null;
            return new TurnRequest
            {
                operation = Operation,
                language = language,
                topic = Topic,
                summary = Summary,
                presentation = Presentation,
                question = latest?.question ?? "",
                answer = latest?.answer ?? "",
                audio = audio == null ? "" : Convert.ToBase64String(audio),
                history = history.ToArray()
            };
        }

        public void Reset()
        {
            Phase = PracticePhase.Setup;
            Topic = Summary = Presentation = Operation = Question = Suggestion = "";
            Elapsed = 0;
            MainQuestions = Attempts = Skips = 0;
            Paused = IsFollowUp = FollowUpAsked = false;
            history.Clear();
        }

        void WaitFor(string operation)
        {
            Phase = PracticePhase.Waiting;
            Paused = false;
            Operation = operation;
        }
    }
}
