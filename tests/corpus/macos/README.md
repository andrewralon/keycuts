# macOS keycut corpus

Hand-written `#!/bin/sh` keycuts as `ShFormat` writes them (docs/macos-port.md, "A keycut on macOS"), plus a few it must read without writing: no header (the launch line alone), a header that disagrees with its launch line, an unclosed quote, and a script that isn't a keycut. `home` is a path saved with `$HOME` (double-quoted). `nasty` and `url-quote` hold `'`, `"`, `$`, backticks, spaces and unicode. Bytes matter: LF only (`.gitattributes`), no extension. `ShCharacterizationTests` snapshots how each one parses and round-trips. Only add to it.
