const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.resolve(__dirname, "..", "src", "miniprogram");

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

const files = walk(root);
const jsFiles = files.filter((file) => file.endsWith(".js"));
const jsonFiles = files.filter((file) => file.endsWith(".json"));
const wxmlFiles = files.filter((file) => file.endsWith(".wxml"));
const wxssFiles = files.filter((file) => file.endsWith(".wxss"));

for (const file of jsFiles) {
  new vm.Script(fs.readFileSync(file, "utf8"), { filename: file });
}

for (const file of jsonFiles) {
  JSON.parse(fs.readFileSync(file, "utf8"));
}

for (const file of wxmlFiles) {
  const source = fs.readFileSync(file, "utf8");
  const stack = [];
  const tags = source.matchAll(/<\/?([a-zA-Z][\w-]*)(?:\s[^<>]*?)?\s*\/?>/g);
  for (const match of tags) {
    const token = match[0];
    const name = match[1];
    if (token.startsWith("</")) {
      const open = stack.pop();
      if (open !== name) {
        throw new Error(`Unbalanced WXML tag in ${file}: expected </${open}>, got </${name}>`);
      }
    } else if (!token.endsWith("/>")) {
      stack.push(name);
    }
  }
  if (stack.length > 0) {
    throw new Error(`Unclosed WXML tag in ${file}: <${stack.at(-1)}>`);
  }
}

for (const file of wxssFiles) {
  const source = fs.readFileSync(file, "utf8");
  const opens = (source.match(/{/g) || []).length;
  const closes = (source.match(/}/g) || []).length;
  if (opens !== closes) {
    throw new Error(`Unbalanced WXSS braces in ${file}`);
  }
}

const appConfig = JSON.parse(fs.readFileSync(path.join(root, "app.json"), "utf8"));
for (const page of appConfig.pages) {
  for (const extension of ["js", "json", "wxml", "wxss"]) {
    const pageFile = path.join(root, `${page}.${extension}`);
    if (!fs.existsSync(pageFile)) {
      throw new Error(`Missing page file: ${pageFile}`);
    }
  }
}

for (const item of appConfig.tabBar.list) {
  for (const iconPath of [item.iconPath, item.selectedIconPath]) {
    const file = path.join(root, iconPath);
    if (!fs.existsSync(file)) {
      throw new Error(`Missing tab icon: ${file}`);
    }
    const buffer = fs.readFileSync(file);
    const pngSignature = "89504e470d0a1a0a";
    if (buffer.subarray(0, 8).toString("hex") !== pngSignature) {
      throw new Error(`Tab icon is not PNG: ${file}`);
    }
    const width = buffer.readUInt32BE(16);
    const height = buffer.readUInt32BE(20);
    if (width !== 81 || height !== 81 || buffer.length > 40 * 1024) {
      throw new Error(`Invalid tab icon dimensions or size: ${file}`);
    }
  }
}

console.log(`PASS  ${jsFiles.length} JavaScript files parse correctly`);
console.log(`PASS  ${jsonFiles.length} JSON files parse correctly`);
console.log(`PASS  ${wxmlFiles.length} WXML files have balanced tags`);
console.log(`PASS  ${wxssFiles.length} WXSS files have balanced braces`);
console.log(`PASS  ${appConfig.pages.length} pages have complete source files`);
console.log(`PASS  ${appConfig.tabBar.list.length * 2} tab icons are valid 81x81 PNG files`);
