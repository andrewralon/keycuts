import AppKit

// The banner tools/mac/check-app.sh shows across the top of the screen while it drives the app:
//   banner <harness pid> <status file> <pause file>
// Pause creates the pause file (the harness waits before its next click or keystroke) and Resume deletes it; Stop ends the
// harness, which puts your desktop back. It never takes focus, so using it doesn't disturb a test. It quits when the harness does.

let args = CommandLine.arguments
let harness: Int32 = args.count == 4 ? (Int32(args[1]) ?? 0) : 0
if harness == 0 {
    FileHandle.standardError.write("usage: banner <harness pid> <status file> <pause file>\n".data(using: .utf8)!)
    exit(2)
}
let statusFile = args[2]
let pauseFile = args[3]

final class Banner: NSObject {
    let panel: NSPanel
    let label = NSTextField(labelWithString: "")
    let pause = NSButton(title: "Pause", target: nil, action: nil)
    let stop = NSButton(title: "Stop", target: nil, action: nil)
    var paused = false

    override init() {
        let screen = NSScreen.main!.frame
        let height: CGFloat = 34
        panel = NSPanel(contentRect: NSRect(x: screen.minX, y: screen.maxY - height, width: screen.width, height: height),
                        styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        super.init()
        panel.level = .screenSaver
        panel.isFloatingPanel = true
        panel.hidesOnDeactivate = false
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.backgroundColor = NSColor(calibratedRed: 0.85, green: 0.35, blue: 0.05, alpha: 1)

        label.textColor = .white
        label.font = .systemFont(ofSize: 14, weight: .semibold)
        label.lineBreakMode = .byTruncatingTail
        label.frame = NSRect(x: 16, y: 7, width: screen.width - 220, height: 20)

        stop.frame = NSRect(x: screen.width - 80, y: 4, width: 68, height: 26)
        pause.frame = NSRect(x: screen.width - 160, y: 4, width: 74, height: 26)
        stop.target = self; stop.action = #selector(stopTest)
        pause.target = self; pause.action = #selector(togglePause)
        for button in [pause, stop] { button.bezelStyle = .rounded }

        panel.contentView!.addSubview(label)
        panel.contentView!.addSubview(pause)
        panel.contentView!.addSubview(stop)
        panel.orderFrontRegardless()

        Timer.scheduledTimer(withTimeInterval: 0.4, repeats: true) { _ in self.tick() }
    }

    func tick() {
        if kill(harness, 0) != 0 { exit(0) }
        let status = (try? String(contentsOfFile: statusFile, encoding: .utf8))?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        label.stringValue = (paused ? "PAUSED  ·  " : "keycuts automated test  ·  hands off the mouse and keyboard  ·  ") + status
        panel.orderFrontRegardless()
    }

    @objc func togglePause() {
        paused.toggle()
        if paused { FileManager.default.createFile(atPath: pauseFile, contents: nil) } else { try? FileManager.default.removeItem(atPath: pauseFile) }
        pause.title = paused ? "Resume" : "Pause"
        tick()
    }

    @objc func stopTest() {
        label.stringValue = "Stopping and putting your desktop back…"
        try? FileManager.default.removeItem(atPath: pauseFile)
        let kids = Process()
        kids.executableURL = URL(fileURLWithPath: "/usr/bin/pkill")
        kids.arguments = ["-TERM", "-P", String(harness)]
        try? kids.run()
        kill(harness, SIGTERM)
    }
}

let app = NSApplication.shared
app.setActivationPolicy(.accessory)
let banner = Banner()
app.run()
