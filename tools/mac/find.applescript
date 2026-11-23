-- find.applescript <pid> <role> <name>: a trailing * in the name matches a prefix. Prints "x,y" of the centre of the first matching element
on run argv
  set pid to item 1 of argv as integer
  set wantRole to item 2 of argv
  set wantName to item 3 of argv
  tell application "System Events"
    set p to first process whose unix id is pid
    set frontmost of p to true
    delay 0.6
    -- Avalonia fills its accessibility tree after the first query: ask again until it has something
    set els to {}
    repeat 10 times
      try
        set els to entire contents of window 1 of p
      end try
      if (count of els) > 0 then exit repeat
      delay 1
    end repeat
    repeat with e in els
      try
        if (role of e as text) is wantRole then
          set n to ""
          try
            set n to name of e as text
          end try
          if n is wantName or (wantName ends with "*" and n starts with (text 1 thru -2 of wantName)) then
            set pp to position of e
            set ss to size of e
            set cx to (item 1 of pp) + ((item 1 of ss) div 2)
            set cy to (item 2 of pp) + ((item 2 of ss) div 2)
            return (cx as text) & "," & (cy as text)
          end if
        end if
      end try
    end repeat
  end tell
  return ""
end run
