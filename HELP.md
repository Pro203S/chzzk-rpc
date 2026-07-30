# Discheese 도움말

문제가 있으신가요? [문제 해결](#문제-해결)을 참고해주세요.  
서버 관련 질문이 있으신가요? [서버 관련](#서버-관련)을 참고해주세요.  
버그가 있으신가요? [Issues](https://github.com/Pro203S/chzzk-rpc/issues)에 제보하세요.

## 문제 해결

### 서버에 연결할 수 없어요.

- 작업관리자에서 Discheese-server가 열려있는지 확인해주세요.  
- 서버에서 사용할 포트가 사용중이지 않은 지 확인해주세요.  

### 로그 확인하기

OS에 따라 로그의 저장 경로가 다릅니다.  
|OS|경로|
|-|-|
|Windows|`%LOCALAPPDATA%` = `C:\Users\<사용자>\AppData\Local`|
|macOS|`~/Library/Application Support`|
|Linux|`$XDG_DATA_HOME` 또는 기본값 `~/.local/share`|

폴더 안에는 `discheese-날짜-시간.log` 파일들이 있습니다.  
오류가 난 시점에 맞는 로그를 확인하시면 됩니다.  

[Issues](https://github.com/Pro203S/chzzk-rpc/issues)에 제보하실때 로그를 같이 보내주시면 버그 해결에 매우 도움이 됩니다.  

## 서버 관련

