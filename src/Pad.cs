using System;
using Rewired;
using UnityEngine;

namespace GK2GlobalStorage
{
    // Gamepad reading for the settings window (Xbox, PlayStation, Steam Deck via Steam Input...).
    // Reads the game's Rewired player directly: the game's own input layer (LazyInput) stops updating while
    // the window has game input switched off. Action ids are the ones LazyBearTechnology.GamepadController maps.
    internal static class Pad
    {
        public const int A = 4;
        public const int B = 5;
        public const int DUp = 12;
        public const int DDown = 13;
        public const int RStick = 19;
        public const int LStick = 20;
        private const int LeftStickY = 1;

        private const float StickThreshold = 0.5f;
        private const float RepeatDelay = 0.35f;
        private const float RepeatPeriod = 0.12f;

        private static bool broken;
        private static int stickDirection;
        private static float nextRepeat;

        private static Player Player
        {
            get
            {
                if (broken || !ReInput.isReady)
                {
                    return null;
                }
                return ReInput.players.GetPlayer(0);
            }
        }

        public static bool Down(int action)
        {
            return Safe(p => p.GetButtonDown(action));
        }

        public static bool Held(int action)
        {
            return Safe(p => p.GetButton(action));
        }

        // Both sticks clicked together (L3 + R3); fires once, on the press that completes the combo.
        public static bool ToggleCombo()
        {
            return Safe(p => p.GetButton(LStick) && p.GetButton(RStick) && (p.GetButtonDown(LStick) || p.GetButtonDown(RStick)));
        }

        // -1 = up, +1 = down, 0 = none. D-pad presses, or the left stick with key-repeat.
        public static int Vertical()
        {
            if (Down(DUp))
            {
                return -1;
            }
            if (Down(DDown))
            {
                return 1;
            }
            float y = 0f;
            Safe(p =>
            {
                y = p.GetAxis(LeftStickY);
                return true;
            });
            int direction = y > StickThreshold ? -1 : (y < -StickThreshold ? 1 : 0);
            float now = Time.unscaledTime;
            if (direction == 0)
            {
                stickDirection = 0;
                return 0;
            }
            if (direction != stickDirection)
            {
                stickDirection = direction;
                nextRepeat = now + RepeatDelay;
                return direction;
            }
            if (now >= nextRepeat)
            {
                nextRepeat = now + RepeatPeriod;
                return direction;
            }
            return 0;
        }

        private static bool Safe(Func<Player, bool> read)
        {
            try
            {
                Player player = Player;
                return player != null && read(player);
            }
            catch (Exception ex)
            {
                broken = true;
                Plugin.ReportOnce("Gamepad input (gamepad controls disabled)", ex);
                return false;
            }
        }
    }
}
