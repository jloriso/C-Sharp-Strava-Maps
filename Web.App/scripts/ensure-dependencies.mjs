import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const appDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const nodeModulesDirectory = path.join(appDirectory, "node_modules");
const installStampPath = path.join(nodeModulesDirectory, ".gpxworldmap-install-stamp");

const [major, minor] = process.versions.node.split(".").map(Number);
const supportedNode =
  (major === 20 && minor >= 19) ||
  (major === 22 && minor >= 12) ||
  major > 22;

if (!supportedNode) {
  console.error(
    `Node.js ${process.versions.node} is not supported. Use Node.js 20.19+ or 22.12+.`,
  );
  process.exit(1);
}

const libc =
  process.platform === "linux"
    ? process.report.getReport().header.glibcVersionRuntime
      ? "gnu"
      : "musl"
    : "";
const hash = createHash("sha256");

for (const filename of ["package.json", "package-lock.json"]) {
  hash.update(filename);
  hash.update(readFileSync(path.join(appDirectory, filename)));
}

const installStamp = [
  process.platform,
  process.arch,
  libc,
  process.versions.node,
  hash.digest("hex"),
].join("-");

let existingStamp;
try {
  existingStamp = readFileSync(installStampPath, "utf8").trim();
} catch (error) {
  if (error.code !== "ENOENT") {
    throw error;
  }
}

if (
  existingStamp === installStamp &&
  existsSync(path.join(nodeModulesDirectory, ".package-lock.json"))
) {
  process.exit(0);
}

console.log("Installing npm packages for this platform...");
const install = spawnSync("npm", ["ci", "--include=optional"], {
  cwd: appDirectory,
  shell: process.platform === "win32",
  stdio: "inherit",
});

if (install.error) {
  console.error(`Could not run npm ci: ${install.error.message}`);
  process.exit(1);
}
if (install.status !== 0) {
  process.exit(install.status ?? 1);
}

writeFileSync(installStampPath, `${installStamp}\n`);
