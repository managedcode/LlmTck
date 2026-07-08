namespace ManagedCode.LlmTck.Hosting;

/// <summary>
/// Self-contained HTML for the LLM TCK mini admin panel served at <c>GET /__llm-tck</c>.
/// The page is a static shell; all data is read from the JSON control endpoints
/// (<c>/__llm-tck/models</c>, <c>/__llm-tck/assertions</c>) and mutated through
/// <c>/__llm-tck/reset</c>, carrying the operator-supplied bearer token.
/// </summary>
internal static class LlmTckAdminPage
{
    public const string ContentType = "text/html; charset=utf-8";

    public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex">
<title>LLM TCK · Control panel</title>
<style>
  :root {
    color-scheme: dark;
    --bg: #0b1014;
    --panel: #111a20;
    --panel-strong: #16242d;
    --text: #ecf4f5;
    --muted: #9fb0b4;
    --line: #28424b;
    --teal: #34d4c4;
    --blue: #7bb6ff;
    --amber: #f0ba5a;
    --red: #f0736a;
    --green: #5fd39a;
    --radius: 12px;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background:
      radial-gradient(1200px 600px at 80% -10%, color-mix(in srgb, var(--teal) 12%, transparent), transparent),
      var(--bg);
    color: var(--text);
    font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    line-height: 1.5;
    -webkit-font-smoothing: antialiased;
  }
  code, .mono { font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace; }
  a { color: var(--teal); }
  .wrap { max-width: 1100px; margin: 0 auto; padding: 28px 24px 64px; }

  header.bar {
    display: flex; align-items: center; gap: 16px; flex-wrap: wrap;
    padding-bottom: 22px; margin-bottom: 22px; border-bottom: 1px solid var(--line);
  }
  .logo { display: flex; align-items: baseline; gap: 10px; }
  .logo b { font-size: 20px; font-weight: 800; letter-spacing: -0.01em; }
  .logo span { color: var(--muted); font-size: 13px; }
  .pill {
    display: inline-flex; align-items: center; gap: 8px; margin-left: auto;
    padding: 6px 12px; border: 1px solid var(--line); border-radius: 999px;
    background: var(--panel); font-size: 13px; font-weight: 600;
  }
  .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--muted); box-shadow: 0 0 0 0 transparent; }
  .pill.ok .dot { background: var(--green); box-shadow: 0 0 10px 1px color-mix(in srgb, var(--green) 70%, transparent); }
  .pill.warn .dot { background: var(--amber); }
  .pill.err .dot { background: var(--red); }

  .toolbar {
    display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap;
    background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius);
    padding: 16px; margin-bottom: 24px;
  }
  .field { display: flex; flex-direction: column; gap: 6px; flex: 1 1 260px; }
  .field label { font-size: 12px; color: var(--muted); font-weight: 600; letter-spacing: 0.02em; }
  input[type=password], input[type=text] {
    background: var(--bg); border: 1px solid var(--line); color: var(--text);
    border-radius: 8px; padding: 10px 12px; font-size: 14px; min-height: 42px; width: 100%;
  }
  input:focus { outline: none; border-color: var(--teal); }
  .btns { display: flex; gap: 10px; flex-wrap: wrap; }
  button {
    appearance: none; cursor: pointer; font: inherit; font-weight: 700;
    border-radius: 8px; min-height: 42px; padding: 0 16px; border: 1px solid var(--line);
    background: var(--panel-strong); color: var(--text); transition: border-color .15s, background .15s, transform .05s;
  }
  button:hover { border-color: color-mix(in srgb, var(--teal) 60%, var(--line)); }
  button:active { transform: translateY(1px); }
  button.primary { background: var(--teal); border-color: var(--teal); color: #06110f; }
  button.danger:hover { border-color: var(--red); color: var(--red); }
  button:disabled { opacity: .5; cursor: not-allowed; }
  .toggle { display: inline-flex; align-items: center; gap: 8px; color: var(--muted); font-size: 13px; user-select: none; }

  .banner {
    display: none; align-items: center; gap: 10px; margin-bottom: 20px; padding: 12px 14px;
    border-radius: 10px; border: 1px solid var(--line); background: var(--panel); font-size: 14px;
  }
  .banner.show { display: flex; }
  .banner.err { border-color: color-mix(in srgb, var(--red) 55%, var(--line)); color: var(--red); }
  .banner.warn { border-color: color-mix(in srgb, var(--amber) 55%, var(--line)); color: var(--amber); }

  .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 26px; }
  .stat {
    background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius);
    padding: 16px; position: relative; overflow: hidden;
  }
  .stat::before { content: ""; position: absolute; inset: 0 auto 0 0; width: 3px; background: var(--accent, var(--muted)); }
  .stat .k { font-size: 12px; color: var(--muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; }
  .stat .v { font-size: 30px; font-weight: 800; line-height: 1.1; margin-top: 6px; font-variant-numeric: tabular-nums; }

  .cols { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.2fr); gap: 20px; }
  @media (max-width: 820px) { .cols { grid-template-columns: 1fr; } }
  .card { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius); overflow: hidden; }
  .card > h2 {
    margin: 0; padding: 14px 18px; font-size: 14px; letter-spacing: 0.03em; text-transform: uppercase;
    color: var(--muted); border-bottom: 1px solid var(--line); display: flex; align-items: center; gap: 8px;
  }
  .card > h2 .count { margin-left: auto; color: var(--text); font-size: 13px; font-weight: 700; }
  .card .body { padding: 6px 0; max-height: 460px; overflow: auto; }

  table { width: 100%; border-collapse: collapse; font-size: 14px; }
  th, td { text-align: left; padding: 10px 18px; }
  th { font-size: 11px; text-transform: uppercase; letter-spacing: 0.05em; color: var(--muted); font-weight: 600; }
  tbody tr { border-top: 1px solid color-mix(in srgb, var(--line) 55%, transparent); }
  td { overflow-wrap: anywhere; }
  td .mono { color: var(--text); }

  .tag {
    display: inline-flex; align-items: center; padding: 2px 9px; border-radius: 999px;
    font-size: 12px; font-weight: 700; border: 1px solid var(--tone, var(--line));
    color: var(--tone, var(--muted)); background: color-mix(in srgb, var(--tone, var(--muted)) 12%, transparent);
    white-space: nowrap;
  }
  .kind-chat { --tone: var(--teal); } .kind-embedding { --tone: var(--blue); }
  .kind-image { --tone: var(--amber); } .kind-audio { --tone: var(--green); } .kind-video { --tone: #c89bff; }
  .ev-matched { --tone: var(--green); } .ev-unmatched { --tone: var(--amber); }
  .ev-modelnotfound { --tone: var(--blue); } .ev-authfailed { --tone: var(--red); }
  .ev-scenarioexhausted { --tone: var(--amber); } .ev-errorreturned { --tone: var(--red); }

  .events { list-style: none; margin: 0; padding: 4px 0; }
  .events li { display: grid; grid-template-columns: auto 1fr; gap: 4px 12px; padding: 10px 18px; border-top: 1px solid color-mix(in srgb, var(--line) 55%, transparent); }
  .events li:first-child { border-top: none; }
  .events li.empty { display: block; padding: 28px 18px; }
  .events .meta { grid-column: 2; color: var(--muted); font-size: 12px; display: flex; gap: 10px; flex-wrap: wrap; margin-top: 8px; }
  .events .msg { grid-column: 2; font-size: 14px; overflow-wrap: anywhere; }
  .events time { color: var(--muted); font-size: 12px; font-variant-numeric: tabular-nums; white-space: nowrap; padding-top: 2px; }

  .io { grid-column: 2; display: grid; gap: 8px; margin-top: 8px; }
  .io-b { border: 1px solid var(--line); border-radius: 8px; background: var(--bg); overflow: hidden; }
  .io-b .io-k { display: block; padding: 4px 10px; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.05em; border-bottom: 1px solid var(--line); }
  .io-req { border-color: color-mix(in srgb, var(--blue) 40%, var(--line)); }
  .io-req .io-k { color: var(--blue); }
  .io-res { border-color: color-mix(in srgb, var(--teal) 42%, var(--line)); }
  .io-res .io-k { color: var(--teal); }
  .io-b pre {
    margin: 0; padding: 8px 10px; font-size: 13px; line-height: 1.45; color: var(--text);
    white-space: pre-wrap; word-break: break-word; max-height: 160px; overflow: auto;
    font-family: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace;
  }

  .empty { padding: 28px 18px; text-align: center; color: var(--muted); font-size: 14px; }
  footer { margin-top: 32px; color: var(--muted); font-size: 12px; text-align: center; }
  footer code { color: var(--text); }
</style>
</head>
<body>
<div class="wrap">
  <header class="bar">
    <div class="logo"><b>LLM&nbsp;TCK</b><span>Control panel</span></div>
    <div class="pill" id="status"><span class="dot"></span><span id="statusText">Connecting…</span></div>
  </header>

  <div class="toolbar">
    <div class="field">
      <label for="token">Bearer token <span style="opacity:.7">(only if the server requires one)</span></label>
      <input id="token" type="password" placeholder="leave empty when no token is configured" autocomplete="off" spellcheck="false">
    </div>
    <div class="btns">
      <button class="primary" id="refresh" type="button">Refresh</button>
      <button class="danger" id="reset" type="button">Reset runtime</button>
      <label class="toggle"><input type="checkbox" id="auto"> auto</label>
    </div>
  </div>

  <div class="banner" id="banner"><span id="bannerText"></span></div>

  <section class="stats" id="stats"></section>

  <div class="cols">
    <div class="card">
      <h2>Models <span class="count" id="modelCount">—</span></h2>
      <div class="body" id="models"></div>
    </div>
    <div class="card">
      <h2>Runtime events <span class="count" id="eventCount">—</span></h2>
      <div class="body"><ul class="events" id="events"></ul></div>
    </div>
  </div>

  <footer>
    Served by <code>MapLlmTck()</code> · data from <code>/__llm-tck/models</code> and <code>/__llm-tck/assertions</code>
  </footer>
</div>

<script>
(function () {
  "use strict";
  var MODEL_KINDS = ["Chat", "Embedding", "Image", "Audio", "Video"];
  var EVENT_KINDS = ["Matched", "Unmatched", "ModelNotFound", "AuthFailed", "ScenarioExhausted", "ErrorReturned"];
  var STAT_CARDS = [
    { key: "totalEvents", label: "Total events", accent: "var(--text)" },
    { key: "totalTokens", label: "Total tokens", accent: "var(--teal)" },
    { key: "inputTokens", label: "Input tokens", accent: "var(--blue)" },
    { key: "outputTokens", label: "Output tokens", accent: "var(--green)" },
    { key: "matched", label: "Matched", accent: "var(--green)" },
    { key: "unmatched", label: "Unmatched", accent: "var(--amber)" },
    { key: "modelNotFound", label: "Model not found", accent: "var(--blue)" },
    { key: "authFailed", label: "Auth failed", accent: "var(--red)" },
    { key: "scenarioExhausted", label: "Scenario exhausted", accent: "var(--amber)" },
    { key: "errorsReturned", label: "Errors returned", accent: "var(--red)" }
  ];
  var TOKEN_KEY = "llm-tck.token";

  var $ = function (id) { return document.getElementById(id); };

  // Storage access can throw in sandboxed iframes or when site data is blocked;
  // degrade gracefully (token just isn't persisted) instead of killing the page.
  function storageGet(key) { try { return localStorage.getItem(key); } catch (e) { return null; } }
  function storageSet(key, value) { try { localStorage.setItem(key, value); } catch (e) { /* ignore */ } }

  var tokenInput = $("token");
  tokenInput.value = storageGet(TOKEN_KEY) || "";
  tokenInput.addEventListener("change", function () {
    storageSet(TOKEN_KEY, tokenInput.value);
    refresh();
  });

  function esc(value) {
    return String(value == null ? "" : value)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }
  function label(value, table) {
    if (typeof value === "number") { return table[value] != null ? table[value] : String(value); }
    return value == null ? "" : String(value);
  }
  function slug(value) { return String(value).toLowerCase().replace(/[^a-z0-9]/g, ""); }
  function formatUsage(usage) {
    if (!usage) { return ""; }
    return "tokens: " + esc(usage.inputTokens || 0) + " in / " +
      esc(usage.outputTokens || 0) + " out / " + esc(usage.totalTokens || 0) + " total";
  }

  function relTime(iso) {
    var t = Date.parse(iso);
    if (isNaN(t)) { return esc(iso); }
    var diff = Math.round((Date.now() - t) / 1000);
    var abs = Math.abs(diff);
    var text;
    if (abs < 5) { text = "just now"; }
    else if (abs < 60) { text = abs + "s ago"; }
    else if (abs < 3600) { text = Math.round(abs / 60) + "m ago"; }
    else if (abs < 86400) { text = Math.round(abs / 3600) + "h ago"; }
    else { text = new Date(t).toLocaleString(); }
    return '<time datetime="' + esc(iso) + '" title="' + esc(new Date(t).toLocaleString()) + '">' + esc(text) + "</time>";
  }

  function api(path) {
    var headers = { "Accept": "application/json" };
    var token = tokenInput.value.trim();
    if (token) { headers.Authorization = "Bearer " + token; }
    return fetch(path, { headers: headers, cache: "no-store" });
  }

  function setStatus(kind, text) {
    var pill = $("status");
    pill.className = "pill " + kind;
    $("statusText").textContent = text;
  }
  function banner(kind, text) {
    var el = $("banner");
    if (!text) { el.className = "banner"; return; }
    el.className = "banner show " + kind;
    $("bannerText").textContent = text;
  }

  function renderStats(summary) {
    $("stats").innerHTML = STAT_CARDS.map(function (c) {
      var v = summary && summary[c.key] != null ? summary[c.key] : 0;
      return '<div class="stat" style="--accent:' + c.accent + '">' +
        '<div class="k">' + esc(c.label) + '</div><div class="v">' + esc(v) + "</div></div>";
    }).join("");
  }

  function renderModels(models) {
    $("modelCount").textContent = models.length;
    if (!models.length) { $("models").innerHTML = '<div class="empty">No models configured.</div>'; return; }
    var rows = models.map(function (m) {
      var kind = label(m.kind, MODEL_KINDS);
      return "<tr><td><span class='mono'>" + esc(m.id) + "</span></td>" +
        "<td><span class='tag kind-" + slug(kind) + "'>" + esc(kind) + "</span></td>" +
        "<td class='mono' style='color:var(--muted)'>" + esc(m.ownedBy) + "</td></tr>";
    }).join("");
    $("models").innerHTML =
      "<table><thead><tr><th>Id</th><th>Kind</th><th>Owned by</th></tr></thead><tbody>" + rows + "</tbody></table>";
  }

  function renderEvents(summary) {
    var events = (summary && summary.events) || [];
    $("eventCount").textContent = events.length;
    if (!events.length) { $("events").innerHTML = '<li class="empty">No runtime events yet.</li>'; return; }
    var items = events.slice().reverse().map(function (e) {
      var kind = label(e.kind, EVENT_KINDS);
      var meta = ["model: " + esc(e.modelId || "—")];
      if (e.scenarioId) { meta.push("scenario: " + esc(e.scenarioId)); }
      if (e.usage) { meta.push(formatUsage(e.usage)); }
      var io = "";
      if (e.request != null || e.response != null) {
        io = "<div class='io'>";
        if (e.request != null) {
          io += "<div class='io-b io-req'><span class='io-k'>request</span><pre>" + esc(e.request) + "</pre></div>";
        }
        if (e.response != null) {
          io += "<div class='io-b io-res'><span class='io-k'>response</span><pre>" + esc(e.response) + "</pre></div>";
        }
        io += "</div>";
      }
      return "<li>" + relTime(e.timestamp) +
        "<span class='tag ev-" + slug(kind) + "'>" + esc(kind) + "</span>" +
        "<div class='msg'>" + esc(e.message || "") + "</div>" + io +
        "<div class='meta'>" + meta.join(" · ") + "</div></li>";
    }).join("");
    $("events").innerHTML = items;
  }

  // Each refresh claims the latest generation; a slower in-flight refresh whose
  // generation is stale (e.g. superseded by a reset) discards its result instead
  // of repainting stale data or clobbering a newer banner. Resolves true only on
  // a successful, still-current load.
  var generation = 0;
  function refresh() {
    var gen = ++generation;
    setStatus("", "Loading…");
    return Promise.all([api("/__llm-tck/models"), api("/__llm-tck/assertions")]).then(function (res) {
      if (gen !== generation) { return false; }
      var modelsRes = res[0], assertRes = res[1];
      if (modelsRes.status === 401 || assertRes.status === 401) {
        setStatus("err", "Unauthorized");
        banner("err", "The control endpoints rejected the token. Enter the configured bearer token above.");
        renderStats(null); renderModels([]); renderEvents(null);
        return false;
      }
      if (!modelsRes.ok || !assertRes.ok) {
        setStatus("err", "Server error");
        banner("err", "The server responded with an error: HTTP " + modelsRes.status + " / " + assertRes.status + ".");
        renderStats(null); renderModels([]); renderEvents(null);
        return false;
      }
      return Promise.all([modelsRes.json(), assertRes.json()]).then(function (data) {
        if (gen !== generation) { return false; }
        var models = data[0], summary = data[1];
        banner("", "");
        setStatus("ok", "Ready");
        renderStats(summary);
        renderModels(Array.isArray(models) ? models : (models.data || []));
        renderEvents(summary);
        return true;
      });
    }).catch(function (err) {
      if (gen !== generation) { return false; }
      setStatus("err", "Offline");
      banner("err", "Could not reach the LLM TCK server: " + err.message);
      return false;
    });
  }

  $("refresh").addEventListener("click", refresh);
  $("reset").addEventListener("click", function () {
    var btn = $("reset");
    btn.disabled = true;
    var headers = {};
    var token = tokenInput.value.trim();
    if (token) { headers.Authorization = "Bearer " + token; }
    fetch("/__llm-tck/reset", { method: "POST", headers: headers }).then(function (res) {
      if (res.status === 401) { banner("err", "Reset was rejected: the token is not accepted."); return; }
      if (!res.ok) { banner("err", "Reset failed with HTTP " + res.status + "."); return; }
      // Reload post-reset state; only confirm if that reload actually succeeded,
      // so a failed refresh keeps its own error banner instead of a false success.
      return refresh().then(function (ok) {
        if (ok) { banner("warn", "Runtime reset — scenario positions and events cleared."); }
      });
    }).catch(function (err) {
      banner("err", "Reset failed: " + err.message);
    }).then(function () { btn.disabled = false; });
  });

  var timer = null;
  $("auto").addEventListener("change", function (e) {
    if (timer) { clearInterval(timer); timer = null; }
    if (e.target.checked) { timer = setInterval(refresh, 4000); }
  });

  refresh();
})();
</script>
</body>
</html>
""";
}
