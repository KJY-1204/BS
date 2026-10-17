"""Compare mob dumps: python tools/compare_mobdumps.py host.log client1.log [client2.log ...]

The host and client builds must run with -bsMobDump. For every dump tick present in both logs this prints
how many mobs each side has and how far apart the positions of the common mobs are."""
import re, sys, math

def parse(path, kind):
    out = {}
    pat = re.compile(r"\[BS\] mobdump " + kind + r" tick=(\d+) n=(\d+) (.*)")
    for line in open(path, encoding="utf-8", errors="replace"):
        m = pat.search(line)
        if not m:
            continue
        tick, n, body = int(m.group(1)), int(m.group(2)), m.group(3)
        mobs = {}
        for item in body.strip().split(";"):
            if "=" not in item:
                continue
            i, xy = item.split("=")
            x, y = xy.split(",")
            mobs[int(i)] = (float(x), float(y))
        out[tick] = (n, mobs)
    return out

server = parse(sys.argv[1], "server")
for client_log in sys.argv[2:]:
    client = parse(client_log, "client")
    print(f"== {client_log.split('/')[-1].split(chr(92))[-1]}: server dump ticks={sorted(server)} client dump ticks={sorted(client)}")
    for tick in sorted(set(server) & set(client)):
        sn, smobs = server[tick]
        cn, cmobs = client[tick]
        common = set(smobs) & set(cmobs)
        dists = [math.dist(smobs[i], cmobs[i]) for i in common]
        only_s = len(set(smobs) - set(cmobs))
        only_c = len(set(cmobs) - set(smobs))
        mx = max(dists) if dists else float("nan")
        mean = sum(dists) / len(dists) if dists else float("nan")
        print(f"  tick {tick}: server n={sn} client n={cn} common={len(common)} onlyServer={only_s} onlyClient={only_c} maxDist={mx:.3f} meanDist={mean:.3f}")
