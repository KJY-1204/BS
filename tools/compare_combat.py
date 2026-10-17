"""Compare combat event dumps: python tools/compare_combat.py host.log client1.log [client2.log ...]

Run the host and the clients with -bsCombatDump. Every combat event is logged with its server time; this
checks that each client received exactly the events the server sent (same kind, projectile and mob, same time).

Only the time span a client was connected for is compared: events before its first received event (it had not
joined yet) and after its last one (it had already quit) are not counted as missing."""
import re
import sys
from collections import Counter

PATTERN = re.compile(r"\[BS\] combatdump (server|client) t=(\d+) (Fire|Hit|End) id=(\d+) mob=(\d+) kill=(\d)")


def parse(path, kind):
    events = []
    for line in open(path, encoding="utf-8", errors="replace"):
        m = PATTERN.search(line)
        if m and m.group(1) == kind:
            events.append((int(m.group(2)), m.group(3), int(m.group(4)), int(m.group(5)), int(m.group(6))))
    return events


def main():
    server = parse(sys.argv[1], "server")
    kinds = Counter(e[1] for e in server)
    print(f"server: {len(server)} events  {dict(kinds)}  kills={sum(e[4] for e in server)}")

    ok = True
    for path in sys.argv[2:]:
        client = parse(path, "client")
        name = path.replace("\\", "/").split("/")[-1]
        if not client:
            print(f"{name}: no events received")
            ok = False
            continue

        first, last = client[0][0], client[-1][0]
        expected = Counter(e for e in server if first <= e[0] <= last)
        got = Counter(client)
        missing = expected - got
        extra = got - expected
        kc = Counter(e[1] for e in client)
        print(f"{name}: {len(client)} events  {dict(kc)}  kills={sum(e[4] for e in client)}  "
              f"compared span {first}..{last} ms  missing={sum(missing.values())} extra={sum(extra.values())}")
        if missing or extra:
            ok = False
            for e in list(missing)[:5]:
                print("   missing", e)
            for e in list(extra)[:5]:
                print("   extra  ", e)
    print("RESULT:", "all clients match the server" if ok else "MISMATCH")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
