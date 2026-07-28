# Discheese

Discheese(디스치즈)는 치지직 방송을 Discord의 RPC에 표시해주는 Chrome 확장입니다.  

## Server

서버를 실행해야 Chrome 확장을 사용할 수 있습니다.

- Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Linux: `~/.config/systemd/user/discheese.service`
- macOS: `~/Library/LaunchAgents/kr.pro203s.discheese.plist`

Linux에서는 사용자 systemd가 활성화된 환경이 필요합니다.

### 서버 자동 실행

컴퓨터를 켤 때 서버를 자동 실행하려면 서버를 `--register-autostart` 인수와 함께 실행하세요.

자동 실행을 삭제하려면 서버를 `--disable-autostart` 인수와 함께 실행하세요.

### 단일 파일 배포

```bash
dotnet publish server/Discheese-server.csproj --configuration Release --output server/bin/publish
```

현재 운영체제용 자체 포함 실행 파일 하나가 `server/bin/publish`에 생성됩니다. 다른 운영체제용 파일은 `--runtime`에 `win-x64`, `linux-x64`, `osx-x64` 또는 `osx-arm64`를 지정해 생성할 수 있습니다.
