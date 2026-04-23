import { RevitClientConnection } from "./SocketClient.js";

// Mutex to serialize all Revit connections - prevents race conditions
// when multiple requests are made in parallel
let connectionMutex: Promise<void> = Promise.resolve();

/**
 * Revit 클라이언트에 연결하고 작업을 실행
 * @param operation 연결 성공 후 실행할 작업 함수
 * @returns 작업 결과
 */
export async function withRevitConnection<T>(
  operation: (client: RevitClientConnection) => Promise<T>
): Promise<T> {
  // Wait for any pending connection to complete before starting a new one
  const previousMutex = connectionMutex;
  let releaseMutex: () => void;
  connectionMutex = new Promise<void>((resolve) => {
    releaseMutex = resolve;
  });
  await previousMutex;

  const revitClient = new RevitClientConnection("localhost", 8080);

  try {
    // Revit 클라이언트에 연결
    if (!revitClient.isConnected) {
      await new Promise<void>((resolve, reject) => {
        const onConnect = () => {
          revitClient.socket.removeListener("connect", onConnect);
          revitClient.socket.removeListener("error", onError);
          resolve();
        };

        const onError = (error: any) => {
          revitClient.socket.removeListener("connect", onConnect);
          revitClient.socket.removeListener("error", onError);
          reject(new Error("connect to revit client failed"));
        };

        revitClient.socket.on("connect", onConnect);
        revitClient.socket.on("error", onError);

        revitClient.connect();

        setTimeout(() => {
          revitClient.socket.removeListener("connect", onConnect);
          revitClient.socket.removeListener("error", onError);
          reject(new Error("Revit 클라이언트 연결 실패"));
        }, 5000);
      });
    }

    // 작업 실행
    return await operation(revitClient);
  } finally {
    // 연결 종료
    revitClient.disconnect();
    // Release the mutex so the next request can proceed
    releaseMutex!();
  }
}
