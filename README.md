# Discheese

Discheese(디스치즈)는 치지직 방송을 Discord의 RPC에 표시해주는 Chrome 확장입니다.  

## Server

> [!IMPORTANT]
> 서버는 기본적으로 58127 포트를 사용합니다.  
> 컴퓨터에서 이 포트를 사용하고 있으면 서버가 열리지 않습니다!

서버를 실행해야 Chrome 확장을 사용할 수 있습니다.

### 서버 설정

서버를 열 때 `--port=<포트번호>` 인수를 전달해 포트를 지정할 수 있습니다.  
`--port=8080` 인수를 전달하면 8080 포트에서 서버가 열리게 됩니다.  

### 서버 자동 실행

컴퓨터를 켤 때 서버를 자동 실행하려면 서버를 `--register-autostart` 인수와 함께 실행하세요.

자동 실행을 삭제하려면 서버를 `--unregister-autostart` 인수와 함께 실행하세요.

자동 실행은 아래 경로 / 레지스트리에 등록됩니다.  

- Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Linux: `~/.config/systemd/user/discheese.service`
- macOS: `~/Library/LaunchAgents/kr.pro203s.discheese.plist`

Linux에서는 사용자 systemd가 활성화된 환경이 필요합니다.
