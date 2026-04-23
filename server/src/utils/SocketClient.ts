import * as net from "net";

export class RevitClientConnection {
  host: string;
  port: number;
  socket: net.Socket;
  isConnected: boolean = false;
  responseCallbacks: Map<string, (response: string) => void> = new Map();
  buffer: string = "";

  constructor(host: string, port: number) {
    this.host = host;
    this.port = port;
    this.socket = new net.Socket();
    this.setupSocketListeners();
  }

  private setupSocketListeners(): void {
    this.socket.on("connect", () => {
      this.isConnected = true;
    });

    this.socket.on("data", (data) => {
      // 수신한 데이터를 버퍼에 추가
      const dataString = data.toString();
      this.buffer += dataString;

      // 완전한 JSON 응답을 파싱 시도
      this.processBuffer();
    });

    this.socket.on("close", () => {
      this.isConnected = false;
    });

    this.socket.on("error", (error) => {
      console.error("RevitClientConnection error:", error);
      this.isConnected = false;
    });
  }

  private processBuffer(): void {
    try {
      // JSON 파싱 시도
      const response = JSON.parse(this.buffer);
      // 파싱에 성공하면 응답을 처리하고 버퍼를 비움
      this.handleResponse(this.buffer);
      this.buffer = "";
    } catch (e) {
      // 파싱에 실패하면 데이터가 완전하지 않을 수 있으므로 더 많은 데이터를 계속 대기
    }
  }

  public connect(): boolean {
    if (this.isConnected) {
      return true;
    }

    try {
      this.socket.connect(this.port, this.host);
      return true;
    } catch (error) {
      console.error("Failed to connect:", error);
      return false;
    }
  }

  public disconnect(): void {
    this.socket.end();
    this.isConnected = false;
  }

  private generateRequestId(): string {
    return Date.now().toString() + Math.random().toString().substring(2, 8);
  }

  private handleResponse(responseData: string): void {
    try {
      const response = JSON.parse(responseData);
      // 응답에서 ID 가져오기
      const requestId = response.id || "default";

      const callback = this.responseCallbacks.get(requestId);
      if (callback) {
        callback(responseData);
        this.responseCallbacks.delete(requestId);
      }
    } catch (error) {
      console.error("Error parsing response:", error);
    }
  }

  public sendCommand(command: string, params: any = {}): Promise<any> {
    return new Promise((resolve, reject) => {
      try {
        if (!this.isConnected) {
          this.connect();
        }

        // 요청 ID 생성
        const requestId = this.generateRequestId();

        // JSON-RPC 표준에 맞는 요청 객체 생성
        const commandObj = {
          jsonrpc: "2.0",
          method: command,
          params: params,
          id: requestId,
        };

        // 콜백 함수 저장
        this.responseCallbacks.set(requestId, (responseData) => {
          try {
            const response = JSON.parse(responseData);
            if (response.error) {
              reject(
                new Error(response.error.message || "Unknown error from Revit")
              );
            } else {
              resolve(response.result);
            }
          } catch (error) {
            if (error instanceof Error) {
              reject(new Error(`Failed to parse response: ${error.message}`));
            } else {
              reject(new Error(`Failed to parse response: ${String(error)}`));
            }
          }
        });

        // 명령 전송
        const commandString = JSON.stringify(commandObj);
        this.socket.write(commandString);

        // 타임아웃 설정
        setTimeout(() => {
          if (this.responseCallbacks.has(requestId)) {
            this.responseCallbacks.delete(requestId);
            reject(new Error(`Command timed out after 2 minutes: ${command}`));
          }
        }, 120000); // 2분 타임아웃
      } catch (error) {
        reject(error);
      }
    });
  }
}
