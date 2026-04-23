import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

export async function registerTools(server: McpServer) {
  // 현재 파일의 디렉터리 경로 가져오기
  const __filename = fileURLToPath(import.meta.url);
  const __dirname = path.dirname(__filename);

  // tools 디렉터리 아래의 모든 파일 읽기
  const files = fs.readdirSync(__dirname);

  // .ts 또는 .js 파일만 필터링하되 index 파일과 register 파일은 제외
  const toolFiles = files.filter(
    (file) =>
      (file.endsWith(".ts") || file.endsWith(".js")) &&
      file !== "index.ts" &&
      file !== "index.js" &&
      file !== "register.ts" &&
      file !== "register.js"
  );

  // 각 도구를 동적으로 가져와 등록
  for (const file of toolFiles) {
    try {
      // 가져오기 경로 구성
      const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;

      // 모듈 동적 가져오기
      const module = await import(importPath);

      // 등록 함수를 찾아 실행
      const registerFunctionName = Object.keys(module).find(
        (key) => key.startsWith("register") && typeof module[key] === "function"
      );

      if (registerFunctionName) {
        module[registerFunctionName](server);
        console.error(`등록된 도구: ${file}`);
      } else {
        console.warn(`경고: 파일 ${file}에서 등록 함수를 찾지 못했습니다`);
      }
    } catch (error) {
      console.error(`도구 ${file} 등록 중 오류 발생:`, error);
    }
  }
}
