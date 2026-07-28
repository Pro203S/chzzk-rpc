# Discheese

Discheese(디스치즈)는 치지직 방송을 Discord의 RPC에 표시해주는 Chrome 확장입니다.  

## Server

서버를 처음 실행하면 현재 사용자의 자동 실행 항목에 스스로 등록됩니다.

- Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Linux: `~/.config/systemd/user/discheese.service`
- macOS: `~/Library/LaunchAgents/kr.pro203s.discheese.plist`

Linux에서는 사용자 systemd가 활성화된 환경이 필요합니다.
