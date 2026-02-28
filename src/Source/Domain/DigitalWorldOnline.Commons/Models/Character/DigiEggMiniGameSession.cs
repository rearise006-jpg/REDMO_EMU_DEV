using System;

namespace DigitalWorldOnline.Commons.Models.Character
{
    /// <summary>
    /// Transient (in-memory) session that tracks the current hatch mini-game state.
    /// Not persisted to DB.
    /// </summary>
    public class DigiEggMiniGameSession
    {
        public bool IsActive { get; private set; } = false;

        /// <summary>
        /// Total bars in this game (e.g. 7 for hatch mini-game).
        /// </summary>
        public int TotalBars { get; private set; } = 0;

        /// <summary>
        /// The index of the bar the server currently expects a click for.
        /// When a click is accepted, ExpectedIndex is advanced.
        /// </summary>
        public int ExpectedIndex { get; private set; } = 0;

        /// <summary>
        /// Clicked flags per bar (true => clicked successfully).
        /// </summary>
        public bool[] Clicked { get; private set; } = Array.Empty<bool>();

        /// <summary>
        /// When the current bar started (used for time-window checks if desired).
        /// </summary>
        public DateTime CurrentBarStart { get; private set; }

        /// <summary>
        /// Duration allowed per bar (ms). Optional — adjust if you have a precise client timing.
        /// </summary>
        public int BarDurationMs { get; private set; } = 2000;

        public void Start(int totalBars, int barDurationMs = 2000)
        {
            if (totalBars <= 0) totalBars = 7;
            TotalBars = totalBars;
            BarDurationMs = barDurationMs;
            Clicked = new bool[TotalBars];
            ExpectedIndex = 0;
            IsActive = true;
            CurrentBarStart = DateTime.UtcNow;
        }

        /// <summary>
        /// Advances to next bar (mark current as missed if not clicked).
        /// Returns true if advanced (i.e. there was a next bar), false if game already ended.
        /// </summary>
        public bool Advance()
        {
            if (!IsActive) return false;

            // Advance expected index
            ExpectedIndex++;

            if (ExpectedIndex >= TotalBars)
            {
                // finished
                End();
                return false;
            }

            CurrentBarStart = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Attempt to register a click for an index.
        /// Returns true if accepted (and marks the click), false otherwise.
        /// Only accepts click if index == ExpectedIndex.
        /// </summary>
        public bool RegisterClick(int index)
        {
            if (!IsActive) return false;
            if (index < 0 || index >= TotalBars) return false;

            if (index != ExpectedIndex)
            {
                // Reject clicks that are for previous or future bars.
                return false;
            }

            Clicked[index] = true;

            // move to next bar
            Advance();
            return true;
        }

        public void End()
        {
            IsActive = false;
        }
    }
}