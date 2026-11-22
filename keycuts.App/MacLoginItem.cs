using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using keycuts.Common;
using keycuts.GUI.ViewModels;

namespace keycuts.GUI
{
    /// <summary>
    /// Open at login on macOS (Phase 6.5): <c>SMAppService.mainAppService</c>, which lists keycuts under System Settings → General →
    /// Login Items (Phase 0.5). Its status: 0 not registered, 1 enabled, 2 needs the user's approval there, 3 not found (read as off).
    /// </summary>
    [SupportedOSPlatform("macos")]
    public sealed class MacLoginItem : ILoginItem
    {
        private static IntPtr Service()
        {
            ObjC.LoadFramework("ServiceManagement");
            return ObjC.Send(ObjC.GetClass("SMAppService"), ObjC.Selector("mainAppService"));
        }

        /// <summary>On when registered, also while it waits for the user's approval in System Settings.</summary>
        public bool IsOn
        {
            get
            {
                using (ObjC.AutoreleasePool())
                {
                    var status = ObjC.SendLong(Service(), ObjC.Selector("status"));
                    return status is 1 or 2;
                }
            }
        }

        public bool NeedsApproval
        {
            get
            {
                using (ObjC.AutoreleasePool())
                {
                    return ObjC.SendLong(Service(), ObjC.Selector("status")) == 2;
                }
            }
        }

        public string? Set(bool on)
        {
            using (ObjC.AutoreleasePool())
            {
                var ok = ObjC.SendBool(Service(), ObjC.Selector(on ? "registerAndReturnError:" : "unregisterAndReturnError:"), out var error);
                return ok ? null : "Couldn’t change Open at login: " + (ObjC.ErrorText(error) ?? "macOS refused.");
            }
        }

        /// <summary>
        /// Whether this start is the login's, so keycuts waits in the menu bar instead of opening its window (issue #294). A login
        /// item registered with <c>SMAppService.mainApp</c> is started by launchd with no launch Apple event at all, so the absence of
        /// an event is the usual sign; <see cref="LoginLaunch.Decide"/> has the rules. False when it can't tell.
        /// </summary>
        public static bool LaunchedAtLogin()
        {
            using (ObjC.AutoreleasePool())
            {
                var manager = ObjC.Send(ObjC.GetClass("NSAppleEventManager"), ObjC.Selector("sharedAppleEventManager"));
                var evt = ObjC.Send(manager, ObjC.Selector("currentAppleEvent"));
                var eventPresent = evt != IntPtr.Zero;
                var legacyLoginItem = false;

                if (eventPresent)
                {
                    // keyAEPropData ('prdt') holds 'lgit' for a login item's launch
                    var property = ObjC.Send(evt, ObjC.Selector("paramDescriptorForKeyword:"), 0x70726474u);
                    legacyLoginItem = property != IntPtr.Zero && ObjC.Send(property, ObjC.Selector("enumCodeValue")) == (IntPtr)0x6C676974;
                }

                var registered = ObjC.SendLong(Service(), ObjC.Selector("status")) == 1;
                return LoginLaunch.Decide(eventPresent, legacyLoginItem, registered, getppid() == 1);
            }
        }

        [DllImport("libc")]
        private static extern int getppid();
    }
}
