"""Read-only Pistoleer/Assault/Sniper PIST-TIME analysis; completeness is not gameplay/visual PASS."""
import argparse
import collections
import json
import math
from pathlib import Path

KEY = ("session", "instance", "seq", "rev", "hit")
REQUIRED = set(KEY) | {"v", "run", "role", "stage", "local", "server", "domain", "unit", "team", "end"}
FLOATS = {"Schedule": ("commit", "due", "nominal", "effective", "anchor", "overshoot", "cooldown"),
          "Dispatch": ("due",), "WriterResult": ("due",), "Result": ("impact",), "Receive": ("impact",),
          "Decision": ("impact", "delay"), "Command": ("renderedAnchor", "normalized", "length", "speed", "delay"),
          "RenderedStart": ("normalized", "length", "speed"), "RawMarker": ("normalized", "renderedAnchor"),
          "TracerDecision": ("impact", "delay", "remaining")}
INTS = {"Result": ("revision", "victim", "kind", "effect", "ordinal", "amount", "hp"),
        "Receive": ("revision", "victim", "kind", "effect", "ordinal", "amount", "hp"),
        "Decision": ("revision", "victim", "kind", "effect", "ordinal"),
        "WriterBefore": ("hp", "damage", "victim", "kind", "effect", "ordinal"),
        "WriterAfter": ("hp", "before", "damage", "victim", "kind", "effect", "ordinal"),
        "VfxResult": ("systems", "active", "manager"), "End": ("sampled", "omitted", "formatFailures"),
        "Count": ("count",), "Command": ("continuity", "crossFade"), "RawMarker": ("continuity",),
        "RenderedStart": ("continuity",), "Marker": ("sourceOnly",), "TracerDecision": ("call",)}
TEXT = {"WriterResult": ("status",), "Result": ("outcome",), "Receive": ("outcome",),
        "Decision": ("status",), "VfxResult": ("status",), "VfxAttempt": ("coldPool",)}
STAGES = set(FLOATS) | set(INTS) | set(TEXT) | {"Begin", "Cancel", "Unavailable", "Retire", "SourceStop", "MarkerSuppressed", "Unregistered",
    "PhaseBind", "PhaseClockRegression", "LateSourceOccurrenceSkipped", "NativeOccurrenceRejected", "NativeOccurrenceAdmitted", "ActiveOverflow", "Baseline", "Pending",
    "NativeEpochApply", "NativeEpochClose", "EpochLifetime", "PhaseHold", "PhaseResume", "NativeLease", "NativeLeaseHit", "NativeRoute"}


def analyze(paths):
    errors, clocks, runs, previous, server_previous = collections.Counter(), collections.Counter(), {}, {}, {}
    groups = collections.defaultdict(list)
    parsed = 0
    for path in paths:
        for n, raw in enumerate(Path(path).read_text(encoding="utf-8-sig").splitlines(), 1):
            if "[PIST-TIME] " not in raw:
                continue
            tokens = [t.split("=", 1) for t in raw.split("[PIST-TIME] ", 1)[1].split() if "=" in t]
            e = dict(tokens)
            if len(e) != len(tokens) or not REQUIRED <= e.keys() or e.get("end") != "1":
                errors["truncated"] += 1
                continue
            try:
                for k in ("instance", "seq", "rev", "hit", "unit", "team"):
                    int(e[k])
                # Historical logs have no type field. Keep their identity unknown rather
                # than retroactively attributing every old record to any observed type.
                if int(e.get("type", "-1")) not in (-1, 0, 1, 2):
                    raise ValueError("observed-type")
                local, server = float(e["local"]), float(e["server"])
                if not math.isfinite(local) or e["v"] != "1" or e["role"] not in ("Host", "Client"):
                    raise ValueError()
                if e["stage"] not in STAGES or e["domain"] not in ("NGO-estimate", "presentation-derived", "unavailable", "phase-observation"):
                    raise ValueError()
                if not math.isfinite(server) and e["domain"] != "unavailable":
                    raise ValueError("server")
                if e.get("formatIncomplete") != "1":
                    if e["stage"] == "NativeRoute":
                        for name in ("epoch", "generation", "target", "targetIsUnit", "callbackLoop", "pending",
                                     "pendingCount", "capacity", "remaining", "sourceOnly", "latestInstance", "latestSeq"):
                            int(e[name])
                        if not e["observation"] or not e["reason"] or e["boundary"] not in (
                                "handoff", "overflow", "cancel", "source-only", "consumed", "rejected", "occurrence-passed"):
                            raise ValueError("route-boundary")
                        if min(int(e["epoch"]), int(e["generation"])) <= 0 or int(e["target"]) < 0:
                            raise ValueError("route-owner")
                        if not 0 <= int(e["pendingCount"]) <= int(e["capacity"]) == 8 or not 0 <= int(e["remaining"]) <= 64:
                            raise ValueError("route-capacity")
                        if any(int(e[name]) not in (0, 1) for name in ("pending", "sourceOnly", "targetIsUnit")):
                            raise ValueError("route-mode")
                        marker = float(e["callbackMarker"])
                        if int(e["callbackLoop"]) < -1 or not (math.isfinite(marker)
                                or int(e["callbackLoop"]) == -1 and math.isnan(marker)):
                            raise ValueError("route-marker")
                    if e["stage"] == "NativeLease":
                        for name in ("ownerInstance", "ownerSeq", "epoch", "generation", "bound", "sourceOnly", "remaining", "hits"):
                            int(e[name])
                        if e["clockSource"] not in ("bind-raw", "phase-high-water") or not e["boundary"]:
                            raise ValueError("lease-clock-source")
                        if not e["observation"] or any(int(e[name]) not in (0, 1) for name in ("bound", "sourceOnly")):
                            raise ValueError("lease-identity")
                        if not 0 <= int(e["remaining"]) <= int(e["hits"]) <= 64:
                            raise ValueError("lease-count")
                        if int(e["bound"]) and any(int(e[name]) <= 0 for name in ("ownerInstance", "ownerSeq", "epoch", "generation")):
                            raise ValueError("lease-owner")
                    if e["stage"] == "NativeLeaseHit":
                        for name in ("found", "expectedHit", "expectedLoop", "consumed", "callbackLoop"):
                            int(e[name])
                        if not e["observation"] or any(int(e[name]) not in (0, 1) for name in ("found", "consumed")):
                            raise ValueError("lease-hit-identity")
                        if int(e["callbackLoop"]) < -1 or (int(e["found"]) and min(int(e["expectedHit"]), int(e["expectedLoop"])) < 0):
                            raise ValueError("lease-loop")
                        for name in ("expectedMarker", "markerServer", "callbackMarker"):
                            value = float(e[name])
                            unavailable = math.isnan(value) and (int(e["found"]) == 0 and name != "callbackMarker"
                                or name == "callbackMarker" and int(e["callbackLoop"]) == -1)
                            if not math.isfinite(value) and not unavailable:
                                raise ValueError("lease-marker")
                        e["leaseSnapshotAvailable"] = str(int(e["found"]) != 0)
                        e["leaseCallbackAvailable"] = str(int(e["callbackLoop"]) >= 0 and math.isfinite(float(e["callbackMarker"])))
                    # New logs expose original clip length independently from the runtime
                    # state duration. Infinity is accepted ONLY as explicit paused duration.
                    # Old length=Infinity stays a schema error, never retroactive PASS.
                    if "stateLength" in e:
                        state_length = float(e["stateLength"])
                        paused = int(e["paused"])
                        speed, rate, clip_length = float(e["speed"]), float(e["stateRate"]), float(e["length"])
                        if paused not in (0, 1) or not all(math.isfinite(x) for x in (speed, rate, clip_length)) or clip_length <= 0:
                            raise ValueError("paused-clip-schema")
                        if paused != int(speed == 0) or math.isnan(state_length) or state_length <= 0:
                            raise ValueError("paused-duration-schema")
                        if math.isinf(state_length) and (state_length < 0 or not paused):
                            raise ValueError("unpaused-infinity")
                    if e["stage"] in ("PhaseHold", "PhaseResume"):
                        if int(e["episode"]) <= 0 or int(e["epoch"]) <= 0 or not e["reason"]:
                            raise ValueError("phase-episode")
                        # unavailable clock may produce NaN target; phase actual must remain real.
                        if not math.isfinite(float(e["actual"])) or not math.isfinite(float(e["dt"])):
                            raise ValueError("phase-observation")
                    for k in ("anchorNormalized", "anchorLength", "anchorSpeed"):
                        if k in e:
                            value = float(e[k])
                            if math.isinf(value):
                                raise ValueError(k)
                    if e["stage"] == "End":
                        for k in ("blue", "red", "sampleExcluded", "unclassifiedScoped", "unscoped",
                                  "active", "peak", "capacity", "population", "activeOverflow",
                                  "sampleOmissions", "tombstones", "tombstoneEvictions"):
                            if k in e and int(e[k]) < 0:
                                raise ValueError(k)
                    for k in FLOATS.get(e["stage"], ()):
                        value = float(e[k])
                        if not math.isfinite(value) and k not in ("anchor", "renderedAnchor", "remaining"):
                            raise ValueError(k)
                    for k in INTS.get(e["stage"], ()):
                        miss_hp = (k == "hp" and e["stage"] in ("Result", "Receive")
                                   and e.get("outcome") == "Miss" and int(e.get("amount", "-1")) == 0
                                   and int(e[k]) == -1)
                        if int(e[k]) < 0 and not miss_hp:
                            raise ValueError(k)
                    if e["stage"] in ("Result", "Receive"):
                        if e["outcome"] == "HitApplied" and (int(e["amount"]) <= 0 or int(e["hp"]) < 0):
                            raise ValueError("hit-payload")
                        if e["outcome"] == "Miss" and int(e["amount"]) != 0:
                            raise ValueError("miss-payload")
                    for k in TEXT.get(e["stage"], ()):
                        if not e[k]:
                            raise ValueError(k)
            except (ValueError, KeyError):
                errors["schema"] += 1
                continue
            parsed += 1
            ident = tuple(e[k] for k in ("session", "run", "role"))
            domain_key = ident + (e["domain"],)
            # Rebased result/presentation estimates are not fresh raw NGO samples.
            # Local realtime regression remains an error; fresh phase clock regression is explicit.
            if local < previous.get(ident, -math.inf):
                errors["local_realtime_regression"] += 1
            if e["stage"] == "PhaseClockRegression":
                errors["fresh_phase_clock_regression"] += 1
            previous[ident] = local
            if math.isfinite(server):
                if server < server_previous.get(domain_key, -math.inf):
                    clocks["rebased_" + e["domain"] + "_regression"] += 1
                server_previous[domain_key] = server
            if e.get("formatIncomplete") == "1":
                errors["format_incomplete"] += 1
                continue
            if e.get("coverageUnknown") == "1":
                errors["coverage_unknown"] += 1
            runs.setdefault(ident, {})[e["stage"]] = e
            e["evidence"] = f"{path}:{n}"
            if int(e["instance"]) > 0:
                groups[(ident, tuple(e[k] for k in KEY))].append(e)
    # Role-local presentation/action axes are aliases, not immutable peer identity.
    # Keep every raw axis and record the exact unique role-local alias explicitly.
    revisions = collections.defaultdict(set)
    for (ident, key), events in groups.items():
        if any(e["stage"] in ("Command", "Schedule") for e in events) and int(key[3]) > 0:
            revisions[(ident, key[:3] + key[4:])].add(key[3])
    resolved = collections.defaultdict(list)
    for (ident, key), events in groups.items():
        known = revisions.get((ident, key[:3] + key[4:]), set())
        raw_revision = key[3]
        if len(known) == 1:
            key = key[:3] + (next(iter(known)),) + key[4:]
        for event in events:
            event["revision_alias"] = {"raw": raw_revision, "role_local": key[3],
                                       "basis": "unique-command-schedule" if len(known) == 1 else "unresolved"}
        resolved[(ident, key)].extend(events)
    groups = resolved
    records = []
    for (ident, key), events in groups.items():
        types = {int(e.get("type", "-1")) for e in events} - {-1}
        if len(types) > 1:
            errors["type_conflict"] += 1
        s = collections.defaultdict(list)
        for e in events:
            s[e["stage"]].append(e)
        for stage in ("Schedule", "Command", "Dispatch", "WriterBefore", "WriterAfter", "WriterResult", "Result", "Receive", "Marker", "VfxAttempt", "VfxResult", "Decision"):
            errors["duplicates"] += max(0, len(s[stage]) - 1)
        stop = bool(s["SourceStop"] or s["Cancel"] or s["Unavailable"])
        need = ["Command"] + ([] if stop else ["Marker", "VfxAttempt", "VfxResult", "Receive", "Decision"])
        if ident[2] == "Host":
            need += ["Schedule"] + ([] if s["Cancel"] or s["Unavailable"] else ["Dispatch", "WriterResult", "Result"])
            if s["WriterResult"] and s["WriterResult"][0].get("status") == "Applied":
                need += ["WriterBefore", "WriterAfter"]
        missing = [stage for stage in need if not s[stage]]
        terminal = runs[ident].get("End")
        unknown = not terminal or "Begin" not in runs[ident] or int(terminal.get("omitted", "0")) > 0 or int(terminal.get("formatFailures", "0")) > 0 or int(terminal.get("activeOverflow", "0")) > 0 or any(errors.values()) or ident[0] == "unavailable" or key[3] == "0"
        metrics = {}
        first = lambda stage: s[stage][0]
        if s["Schedule"] and s["Dispatch"]:
            metrics["dispatch_minus_due"] = float(first("Dispatch")["server"]) - float(first("Schedule")["due"])
            metrics["schedule"] = {k: first("Schedule").get(k) for k in ("nominal", "effective", "commit", "due", "anchor", "overshoot")}
        if s["Dispatch"] and s["WriterBefore"] and s["WriterAfter"] and s["WriterResult"]:
            # Same-host interpolation only; NGO update and dispatch/write bracket uncertainty retained.
            d, w, a, r = [first(stage) for stage in ("Dispatch", "WriterBefore", "WriterAfter", "WriterResult")]
            metrics["writer_server_estimate"] = float(d["server"]) + float(w["local"]) - float(d["local"])
            metrics["writer_call_local_duration"] = float(a["local"]) - float(w["local"])
            metrics["clock_anchor_residual"] = float(r["server"]) - (float(d["server"]) + float(r["local"]) - float(d["local"]))
            metrics["writer_server_bracket"] = [float(d["server"]), float(r["server"])]
        if s["RawMarker"] and s["Command"]:
            anchor = float(first("Command").get("renderedAnchor", "nan"))
            if not math.isfinite(anchor) and s["RenderedStart"]:
                anchor = float(first("RenderedStart")["local"])
            if math.isfinite(anchor):
                metrics["marker_minus_rendered_anchor"] = float(first("RawMarker")["local"]) - anchor
            anchor_data = first("Command")
            if s["RenderedStart"] and not math.isfinite(float(anchor_data.get("renderedAnchor", "nan"))):
                anchor_data = {"anchorNormalized": first("RenderedStart")["normalized"],
                               "anchorLength": first("RenderedStart")["length"], "anchorSpeed": first("RenderedStart")["speed"]}
            an, length, speed = [float(anchor_data.get(k, "nan")) for k in ("anchorNormalized", "anchorLength", "anchorSpeed")]
            if all(math.isfinite(x) for x in (an, length, speed)) and speed > 0:
                metrics["marker_clip_elapsed_from_anchor"] = (float(first("RawMarker")["normalized"]) - an) * length / speed
        impact_event = first("Result") if s["Result"] else first("Receive") if s["Receive"] else None
        if impact_event:
            impact = float(impact_event["impact"])
            for stage in ("RawMarker", "Marker", "VfxAttempt", "VfxResult", "Receive", "Decision"):
                if s[stage] and math.isfinite(float(first(stage)["server"])):
                    metrics[stage + "_server_estimate_minus_impact"] = float(first(stage)["server"]) - impact
                    delay_event = first("Decision") if s["Decision"] else first("Command") if s["Command"] else None
                    if delay_event:
                        metrics[stage + "_minus_impact_plus_delay"] = float(first(stage)["server"]) - impact - float(delay_event["delay"])
            metrics["clock_boundary"] = "NGO receipt vs presentation-derived estimate; not exact visual/HP time"
        # Native evidence is a separate axis: a continuous Command anchor can replace
        # a new RenderedStart, but canonical delivery alone cannot certify native timing.
        native_missing = []
        if types == {2} and not stop:
            if not s["RawMarker"]:
                native_missing.append("RawMarker")
            if "marker_clip_elapsed_from_anchor" not in metrics or "marker_minus_rendered_anchor" not in metrics:
                native_missing.append("NativeAnchor")
        records.append({"role": ident[2], "run": ident[1], "key": dict(zip(KEY, key)),
                        "unit_type": next(iter(types)) if len(types) == 1 else None,
                        "completeness": "unknown" if unknown else "incomplete" if missing else "complete",
                        "source_completeness": "incomplete" if any(not s[x] for x in ("Marker", "VfxAttempt", "VfxResult")) and not stop else "observed-or-stopped",
                        "native_timing_completeness": "not-requested" if types != {2} else "unknown" if unknown else "stopped" if stop else "incomplete" if native_missing else "observed",
                        "native_timing_missing": native_missing,
                        "missing": missing, "intentional_stop": stop, "metrics": metrics, "events": events})
    peers, cycles = collections.defaultdict(set), collections.defaultdict(list)
    if any(errors.values()):
        for record in records:
            record["completeness"] = "unknown"
    for r in records:
        peers[tuple(r["key"][k] for k in KEY if k != "rev")].add(r["role"])
        for event in r["events"]:
            if event["stage"] == "RawMarker":
                cycles[(r["key"]["session"], r["run"], r["role"], r["key"]["instance"], event["continuity"])].append(event)
    intervals = []
    for identity, markers in cycles.items():
        markers.sort(key=lambda e: float(e["local"]))
        intervals += [{"identity": identity, "local_interval": float(b["local"]) - float(a["local"]),
                       "normalized_cycles": float(b["normalized"]) - float(a["normalized"]),
                       "from": a["evidence"], "to": b["evidence"]} for a, b in zip(markers, markers[1:])]
    canonical = collections.defaultdict(lambda: collections.defaultdict(set))
    for r in records:
        for e in r["events"]:
            if e["stage"] not in ("Result", "Receive"):
                continue
            immutable = tuple(e[k] for k in ("session", "instance", "seq", "hit"))
            payload = tuple(e[k] for k in ("revision", "victim", "kind", "effect", "ordinal", "outcome", "amount", "hp")) + (float(e["impact"]), int(e.get("type", "-1")))
            canonical[immutable][(r["role"], e["stage"])].add(payload)
    canonical_pairs = 0
    for by_channel in canonical.values():
        host_result = by_channel.get(("Host", "Result"), set())
        client_receive = by_channel.get(("Client", "Receive"), set())
        if host_result and client_receive:
            if host_result != client_receive or len(host_result) != 1:
                errors["canonical_conflict"] += 1
            else:
                canonical_pairs += 1
        elif host_result or client_receive:
            errors["canonical_unpaired"] += 1
        host_receive = by_channel.get(("Host", "Receive"), set())
        if host_result and host_receive and host_result != host_receive:
            errors["canonical_conflict"] += 1
    if any(errors.values()):
        for record in records:
            record["completeness"] = "unknown"
    return {"parsed": parsed, "errors": {k: v for k, v in errors.items() if v}, "clock_counters": dict(clocks),
            "source_incomplete_records": sum(r["source_completeness"] == "incomplete" for r in records),
            "canonical_paired_payloads": canonical_pairs, "records": records,
            "scope_records_by_type": dict(collections.Counter("unknown" if r["unit_type"] is None else str(r["unit_type"]) for r in records)),
            "paired_keys": sum(v == {"Host", "Client"} for v in peers.values()), "local_marker_intervals": intervals,
            "unpaired_unknown": sum(len(v) != 2 for v in peers.values()),
            "boundary": "64 detailed attack scopes plus bounded all-scope critical lifecycle; raw revisions retained; no cross-peer clock subtraction or timing PASS"}


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("logs", nargs="*")
    ap.add_argument("--fixtures", type=Path)
    a = ap.parse_args()
    if a.fixtures:
        checks = {}
        for case in ("normal", "assault", "sniper", "snipermissingnative", "late", "cancel", "missing", "duplicates", "truncated", "regression", "arrival", "drop", "schema", "terminal", "format", "quota", "mixed", "miss", "sourceonly", "axes", "payload", "victim", "rebased", "phaseclock", "endnumeric", "evicted", "paused", "badinf", "reentry", "episode", "exactcoverage", "lease", "leaseinvalid", "leaseoverflow", "route", "routeinvalid", "routeoverflow"):
            r = analyze([a.fixtures / f"{case}-{role}.txt" for role in ("Host", "Client")])
            expected = {"duplicates": "duplicates", "truncated": "truncated", "regression": "local_realtime_regression", "schema": "schema", "terminal": "schema", "format": "format_incomplete", "payload": "canonical_conflict", "victim": "canonical_conflict", "phaseclock": "fresh_phase_clock_regression", "endnumeric": "schema", "evicted": "coverage_unknown", "badinf": "schema"}.get(case)
            if case == "leaseinvalid": expected = "schema"
            if case == "leaseoverflow": expected = "format_incomplete"
            if case == "routeinvalid": expected = "schema"
            if case == "routeoverflow": expected = "format_incomplete"
            ok = r["paired_keys"] == 1 and (bool(r["errors"].get(expected)) if expected else not r["errors"])
            desired = "incomplete" if case in ("missing", "exactcoverage") else "unknown" if case == "drop" else "complete"
            if not expected:
                ok &= all(e["completeness"] == desired for e in r["records"])
            else:
                ok &= all(e["completeness"] != "complete" for e in r["records"])
            if case == "normal":
                ok &= all("marker_minus_rendered_anchor" in e["metrics"] for e in r["records"])
            if case == "assault":
                ok &= r["canonical_paired_payloads"] == 1 and r["scope_records_by_type"] == {"1": 2}
                ok &= any(abs(float(e["nominal"]) - 5 / 30) < 1e-7
                          for record in r["records"] for e in record["events"] if e["stage"] == "Schedule")
            if case.startswith("sniper"):
                ok &= r["canonical_paired_payloads"] == 1 and r["scope_records_by_type"] == {"2": 2}
                ok &= any(abs(float(e["nominal"]) - 52 / 30) < 1e-7 and float(e["cooldown"]) == 3
                          for record in r["records"] for e in record["events"] if e["stage"] == "Schedule")
                ok &= all(e["native_timing_completeness"] == ("incomplete" if case == "snipermissingnative" else "observed") for e in r["records"])
            if case == "late":
                ok &= any(e["metrics"].get("dispatch_minus_due", 0) > 0 for e in r["records"])
            if case == "axes":
                ok &= r["canonical_paired_payloads"] == 1
            if case == "rebased":
                ok &= bool(r["clock_counters"])
            if case == "exactcoverage":
                ok &= r["canonical_paired_payloads"] == 1 and r["source_incomplete_records"] == 2
            if case == "paused":
                ok &= all("Command" not in e["missing"] for e in r["records"])
            if case == "episode":
                ok &= sum(x["stage"] == "PhaseHold" for e in r["records"] for x in e["events"]) == 4
            checks[case] = {"ok": bool(ok), "parsed": r["parsed"], "errors": r["errors"]}
        print(json.dumps(checks, indent=2))
        return 0 if all(c["ok"] for c in checks.values()) else 1
    r = analyze(a.logs)
    print(json.dumps(r, indent=2, allow_nan=False))
    return 0 if r["parsed"] and not r["errors"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
