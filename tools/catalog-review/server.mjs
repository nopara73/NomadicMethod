import { createReadStream } from "node:fs";
import { mkdir, readFile, rename, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";

const directory = path.dirname(fileURLToPath(import.meta.url));
const repository = path.resolve(directory, "..", "..");
const assets = path.join(repository, "NomadicMethod", "Assets");
const reviewPath = path.join(repository, "docs", "catalog-audit", "user_exercise_reviews.json");
const catalogPath = path.join(assets, "exercises.json");
const revisionSource = await readFile(
  path.join(repository, "NomadicMethod", "Services", "CatalogMigrationRules.cs"),
  "utf8",
);
const catalogRevision = Number(
  revisionSource.match(/CurrentCatalogRevision\s*=\s*(\d+)/)?.[1] ?? 0,
);
const portArgument = process.argv.indexOf("--port");
const port = portArgument >= 0 ? Number(process.argv[portArgument + 1]) : 4173;

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "127.0.0.1"}`);
    if (request.method === "GET" && url.pathname === "/api/catalog") {
      return serveFile(request, response, catalogPath, "application/json; charset=utf-8");
    }
    if (request.method === "GET" && url.pathname === "/api/reviews") {
      return serveReviews(response);
    }
    if (request.method === "POST" && url.pathname === "/api/reviews") {
      return saveReviews(request, response);
    }
    if (request.method === "GET" && url.pathname.startsWith("/media/")) {
      const relative = decodeURIComponent(url.pathname.slice("/media/".length));
      const target = path.resolve(assets, relative);
      if (!target.startsWith(`${assets}${path.sep}`)) return send(response, 403, "Forbidden");
      return serveFile(request, response, target, contentType(target));
    }
    const staticFiles = {
      "/": "index.html",
      "/index.html": "index.html",
      "/app.js": "app.js",
      "/styles.css": "styles.css",
    };
    if (request.method === "GET" && staticFiles[url.pathname]) {
      const file = staticFiles[url.pathname];
      return serveFile(request, response, path.join(directory, file), contentType(file));
    }
    send(response, 404, "Not found");
  } catch (error) {
    console.error(error);
    send(response, 500, "Server error");
  }
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Nomadic Method catalog review: http://127.0.0.1:${port}`);
  console.log(`Reviews save to: ${reviewPath}`);
});

async function serveReviews(response) {
  try {
    const saved = JSON.parse(await readFile(reviewPath, "utf8"));
    saved.catalogRevision = catalogRevision;
    send(response, 200, JSON.stringify(saved), "application/json; charset=utf-8");
  } catch {
    send(response, 200, JSON.stringify(newDocument()), "application/json; charset=utf-8");
  }
}

async function saveReviews(request, response) {
  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (body.length > 2_000_000) return send(response, 413, "Too large");
  }
  const document = JSON.parse(body);
  if (document?.schemaVersion !== 1 || !Array.isArray(document.reviews)) {
    return send(response, 400, "Invalid review document");
  }
  document.catalogRevision = catalogRevision;
  await mkdir(path.dirname(reviewPath), { recursive: true });
  const temporary = `${reviewPath}.tmp`;
  await writeFile(temporary, `${JSON.stringify(document, null, 2)}\n`, "utf8");
  await rename(temporary, reviewPath);
  send(response, 204, "");
}

async function serveFile(request, response, file, type) {
  const information = await stat(file);
  const range = request.headers.range;
  if (range) {
    const match = /^bytes=(\d*)-(\d*)$/.exec(range);
    if (!match) return send(response, 416, "Invalid range");
    const start = match[1] ? Number(match[1]) : 0;
    const end = match[2] ? Math.min(Number(match[2]), information.size - 1) : information.size - 1;
    response.writeHead(206, {
      "accept-ranges": "bytes",
      "content-range": `bytes ${start}-${end}/${information.size}`,
      "content-length": end - start + 1,
      "content-type": type,
    });
    createReadStream(file, { start, end }).pipe(response);
    return;
  }
  response.writeHead(200, {
    "accept-ranges": "bytes",
    "content-length": information.size,
    "content-type": type,
  });
  createReadStream(file).pipe(response);
}

function newDocument() {
  return {
    schemaVersion: 1,
    catalogRevision,
    updatedAtUtc: new Date().toISOString(),
    lastExerciseId: null,
    reviews: [],
  };
}

function contentType(file) {
  return {
    ".html": "text/html; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".json": "application/json; charset=utf-8",
    ".mp4": "video/mp4",
    ".png": "image/png",
  }[path.extname(file).toLowerCase()] ?? "application/octet-stream";
}

function send(response, status, body, type = "text/plain; charset=utf-8") {
  response.writeHead(status, { "content-type": type });
  response.end(body);
}
