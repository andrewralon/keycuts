#nullable enable
using System.Windows;
using System.Windows.Input;

namespace keycuts.Tests
{
    /// <summary>
    /// A keyboard that reports only the modifier keys a test asks for. A key event built on it tells a handler that reads
    /// <c>e.KeyboardDevice.Modifiers</c> exactly that, whatever the person at the machine is holding; <c>Keyboard.Modifiers</c> and
    /// <c>Keyboard.IsKeyDown</c> read the physical keyboard, so a test that goes through them fails when Shift or Ctrl is held
    /// during the run. It must be created on the thread that owns the window, as <see cref="InputManager.Current"/> is per thread.
    /// </summary>
    sealed class FakeKeyboardDevice : KeyboardDevice
    {
        private readonly ModifierKeys modifiers;

        public FakeKeyboardDevice(ModifierKeys modifiers = ModifierKeys.None)
            : base(InputManager.Current)
        {
            this.modifiers = modifiers;
        }

        // Where the focus is and which window is active are not what these tests fake, so they follow the real device
        public override IInputElement Target
        {
            get { return Keyboard.PrimaryDevice.Target; }
        }

        public override PresentationSource ActiveSource
        {
            get { return Keyboard.PrimaryDevice.ActiveSource; }
        }

        protected override KeyStates GetKeyStatesFromSystem(Key key)
        {
            var down = key switch
            {
                Key.LeftShift or Key.RightShift => modifiers.HasFlag(ModifierKeys.Shift),
                Key.LeftCtrl or Key.RightCtrl => modifiers.HasFlag(ModifierKeys.Control),
                Key.LeftAlt or Key.RightAlt => modifiers.HasFlag(ModifierKeys.Alt),
                Key.LWin or Key.RWin => modifiers.HasFlag(ModifierKeys.Windows),
                _ => false
            };

            return down ? KeyStates.Down : KeyStates.None;
        }
    }
}
