-- Prints the text fields ("FIELD name = value") and the non-empty static texts ("TEXT value") of the app window, for tools/mac/check-app.sh.
-- osascript tools/mac/read.applescript <pid>

on run argv
  set pid to item 1 of argv as integer
  set out to ""
  tell application "System Events"
    set p to first process whose unix id is pid
    set frontmost of p to true
    delay 0.6
    set els to entire contents of window 1 of p
    repeat with e in els
      try
        set r to role of e as text
        set n to ""
        set v to ""
        try
          set n to name of e as text
        end try
        try
          set v to value of e as text
        end try
        if r is "AXTextField" then set out to out & "FIELD " & n & " = " & v & linefeed
        if r is "AXStaticText" and v is not "" and v is not "missing value" then set out to out & "TEXT " & v & linefeed
      end try
    end repeat
  end tell
  return out
end run
