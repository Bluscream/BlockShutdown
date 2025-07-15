schtasks /Change /TN "\blu\elevated\Block Shutdown" /ENABLE
schtasks /Run /TN "\blu\elevated\Block Shutdown"

@REM BlockShutdown.exe --block --ask --loop --aggressive --prevent-sleep --block-power-keys --enable-events --enable-logging --abort-interval=1 --keep-alive-interval=1 --power-state-interval=1 --emergency-hotkey="Ctrl+Alt+Shift+S" --event-directory-base="Programs" --log-level="Debug"
