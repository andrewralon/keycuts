import AppKit

// A plain white window behind another one, for tools/mac/check-app.sh: black where a window should be see-through can't pass for a
// dark desktop against this. It belongs to the check, which stops it by its pid.
//   whitewindow <x> <y> <width> <height>      in screen points, top left origin, as System Events reports windows
let a = CommandLine.arguments
guard a.count == 5, let x = Double(a[1]), let y = Double(a[2]), let w = Double(a[3]), let h = Double(a[4]) else {
    FileHandle.standardError.write("usage: whitewindow <x> <y> <width> <height>\n".data(using: .utf8)!)
    exit(2)
}
let app = NSApplication.shared
app.setActivationPolicy(.accessory)
let screenHeight = NSScreen.screens.first!.frame.height
let window = NSWindow(contentRect: NSRect(x: x, y: screenHeight - y - h, width: w, height: h), styleMask: [.borderless], backing: .buffered, defer: false)
window.backgroundColor = .white
window.isOpaque = true
window.hasShadow = false
window.level = .normal
window.orderFront(nil)
window.orderBack(nil)   // behind every other window, so the window being checked is always in front of it
app.run()
