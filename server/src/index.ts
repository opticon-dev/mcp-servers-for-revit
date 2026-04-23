#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools } from "./tools/register.js";

// 서비스 인스턴스 생성
const server = new McpServer({
  name: "mcp-server-for-revit",
  version: "1.0.0",
});

// 서버 시작
async function main() {
  // 도구 등록
  await registerTools(server);

  // 전송 계층에 연결
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Revit MCP Server start success");
}

main().catch((error) => {
  console.error("Error starting Revit MCP Server:", error);
  process.exit(1);
});
