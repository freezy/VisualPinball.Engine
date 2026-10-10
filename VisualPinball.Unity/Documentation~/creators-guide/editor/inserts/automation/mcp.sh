#!/bin/sh
# usage: mcp.sh <tool> '<json-args>' | mcp.sh <tool> @argsfile.json | mcp.sh --resource <uri> | mcp.sh --list
# Raw JSON-RPC to the "MCP for Unity" server at 127.0.0.1:8088, for sessions without the mcp__unity-mcp__ tools.
# INSTANCE (default vpe-terminator2; the other is LookDev~) names the editor, resolved to Name@hash on each call.
URL=http://127.0.0.1:8088/mcp
HDR='Content-Type: application/json'
ACC='Accept: application/json, text/event-stream'
NAME=${INSTANCE:-vpe-terminator2}
SID=$(curl -s -D - -o /dev/null -H "$HDR" -H "$ACC" $URL -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"curl","version":"1"}}}' | grep -i '^mcp-session-id' | awk '{print $2}' | tr -d '\r')
curl -s -o /dev/null -H "$HDR" -H "$ACC" -H "Mcp-Session-Id: $SID" $URL -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'
call() { curl -s -H "$HDR" -H "$ACC" -H "Mcp-Session-Id: $SID" $URL -d "$1" | sed -n 's/^data: //p'; }
if [ "$1" = "--resource" ]; then
  BODY="{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"resources/read\",\"params\":{\"uri\":\"$2\"}}"
elif [ "$1" = "--list" ]; then
  BODY='{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
else
  INST=$(call '{"jsonrpc":"2.0","id":9,"method":"resources/read","params":{"uri":"mcpforunity://instances"}}' | python -c "
import sys,json
name='$NAME'
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    d=json.loads(line)
    if 'result' not in d: continue
    inner=json.loads(d['result']['contents'][0]['text'])
    for i in inner['instances']:
        if i['name']==name or i['id']==name: print(i['id']); break
")
  ARGS=${2:-{\}}
  case "$ARGS" in @*) ARGS=$(cat "${ARGS#@}");; esac
  ARGS=$(printf '%s' "$ARGS" | python -c "import sys,json; a=json.load(sys.stdin); a.setdefault('unity_instance','$INST'); print(json.dumps(a))")
  BODY="{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"$1\",\"arguments\":$ARGS}}"
fi
call "$BODY" | python -c "
import sys,json
for line in sys.stdin:
    line=line.strip()
    if not line: continue
    d=json.loads(line)
    if 'result' not in d: continue
    r=d['result']
    if 'content' in r:
        for c in r['content']:
            t=c.get('text','')
            try: print(json.dumps(json.loads(t),indent=1))
            except Exception: print(t)
    else: print(json.dumps(r,indent=1))
"
