#!/usr/bin/env python3
"""Only newly owner-consented ORIGINAL dev-L/M/N pixels + blank UI may be published.

Existing 180-file approved public site stays byte-identical apart from homepage
and public digest manifest. Prospective O/P/Q, source trace, label JSON and
learned model parameters are never admissible.
"""
import argparse
import hashlib
import json
import re
import struct
import sys
from pathlib import Path
from zipfile import ZipFile

sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
from tools.m221.publish_consented_public import verify as verify_original_site

SOURCE_ARCHIVE_SHA256 = "400b9df3233ab0cf1af20d2e57d9cef84ffb48bbdd7a40f98bc4d4700f1c303d"
PUBLIC_TREE_SHA256 = "PIN_AFTER_M224_BUILD"
SOURCE_SEED = "argus-m223-fresh-development-20261018-v1"
PROSPECTIVE_SEED = "argus-m223-sealed-prospective-20261018-v1"
RUNS = ("dev-l","dev-m","dev-n")
PNG_MAGIC=b"\x89PNG\r\n\x1a\n"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def admitted_originals(source):
    require(source.is_file() and source.stat().st_size < 24_000_000,
            "Missing or suspiciously large approved development original archive.")
    require(sha(source.read_bytes()) == SOURCE_ARCHIVE_SHA256,
            "Wrong source ZIP: original M2.23 L/M/N immutable archive SHA differs.")
    with ZipFile(source) as z:
        names=z.namelist()
        require(len(names)==len(set(names))==59,
                "Only 54 original frames, 3 BLANK reviewers, index and plan are allowed.")
        require(all(not n.startswith("/") and ".." not in Path(n).parts and not n.endswith("/")
                    for n in names), "Archive has unsafe path.")
        require(z.testzip() is None, "Corrupt authentic source archive.")
        plan=json.loads(z.read("study-manifest.json"))
        require(plan.get("schemaVersion")==1 and plan.get("seed")==SOURCE_SEED and
                plan.get("sourceRole")=="development" and plan.get("partition")=="development" and
                plan.get("runIds")==list(RUNS) and plan.get("numOriginalImages")==54 and
                plan.get("humanGroundTruth")=="ALL_UNKNOWN" and
                plan.get("trainingAuthorized") is True and
                plan.get("prospectiveSealIntact") is False,
                "Not the originally frozen L/M/N-only development source protocol.")
        allowed={"index.html","study-manifest.json"}
        admitted={}
        images={}
        for run in plan["runs"]:
            run_id=run.get("runId")
            require(run_id in RUNS and len(run.get("images",[]))==18,
                    "Unexpected source study run/image count.")
            path=f"annoteren/{run_id}/index.html"
            allowed.add(path)
            html=z.read(path).decode("utf-8")
            require(html.count("<script>")==html.count("</script>")==1 and
                    "const study=" in html and "localStorage.setItem(storageKey" in html and
                    '"argus-m223-player-point-"' in html and
                    "fetch(" not in html and "playerX" not in html and
                    "threatLocations" not in html and "run-01" not in html and
                    '"state":"visible"' not in html and
                    "study.images.length!==18" in html,
                    "Public L/M/N reviewer has data-leaking fields or is not fully UNKNOWN.")
            study=re.search(r"const study=(\{.*?\});\s*const W=",html,re.S)
            require(study is not None,"Unable to verify browser's exact source image list.")
            expected_study={"schemaVersion":1,"seed":SOURCE_SEED,"sourceSeed":SOURCE_SEED,
                            "runId":run_id,"partition":"development","images":run["images"]}
            require(json.loads(study.group(1))==expected_study,
                    "Reviewer embedded steps/sha differ from frozen source.")
            admitted[path]=z.read(path)
            digests={}
            for frame in run["images"]:
                step=frame.get("step")
                filename=frame.get("file")
                require(type(step)==int and 0<=step<=180 and
                        filename==f"frame-{step:04d}.png" and
                        re.fullmatch(r"[0-9a-f]{64}",frame.get("sha256","")),
                        "Frozen original step/filename/hash is invalid.")
                full=f"annoteren/{run_id}/{filename}"
                allowed.add(full)
                raw=z.read(full)
                require(24<=len(raw)<2_000_000 and raw[:8]==PNG_MAGIC and
                        struct.unpack(">II",raw[16:24])==(390,844) and
                        sha(raw)==frame["sha256"],
                        "Authentic L/M/N frame SHA/390x844 dimensions invalid.")
                digests[filename]=frame["sha256"]
                admitted[full]=raw
            require(len(digests)==18,"Duplicate sampled source step in one run.")
            images[run_id]=digests
        require(set(images)==set(RUNS) and len(admitted)==57 and set(names)==allowed,
                "Unexpected prospective screenshot, user-exported label or source trace admitted.")
        return admitted,images


def tree_hash(files):
    return sha("".join(name+"\x00"+sha(raw)+"\n"
                       for name,raw in sorted(files.items())).encode())


def verify(site,parent=None,admitted=None,require_pinned=True):
    require(site.is_dir(),"Published site folder missing.")
    files={p.relative_to(site).as_posix():p.read_bytes()
           for p in site.rglob("*") if p.is_file() and not p.is_symlink()}
    require(len(files)==237 and files.get(".nojekyll")==b"",
            "Site has wrong total source count or nojekyll state.")
    manifest=json.loads(files["published-development-manifest.json"])
    require(manifest.get("m223PointRoutes")==[f"annoteren/{r}/" for r in RUNS] and
            manifest.get("m223SourceStudySeed")==SOURCE_SEED and
            manifest.get("m223HumanTruth")=="ALL_UNKNOWN" and
            set(manifest.get("m223Screenshots",{}))==set(RUNS),
            "Public manifest does not authorize exactly blank L/M/N references.")
    require(PROSPECTIVE_SEED not in files["index.html"].decode() and
            "dev-o" not in files["index.html"].decode() and
            "dev-p" not in files["index.html"].decode() and
            "dev-q" not in files["index.html"].decode(),
            "Prospective held-out run leaked to homepage.")
    require(len(list(site.glob("annoteren/*/frame-*.png")))==216 and
            len(list(site.glob("annoteren/*/index.html")))==18,
            "Expected 216 previously-approved/new original screenshots and 18 empty reviewers.")
    require(len(list(site.rglob("*.json")))==1 and
            not list(site.rglob("*.jsonl")) and not list(site.rglob("*.zip")) and
            not list(site.rglob("*.weights")),
            "Unexpected research trace/private JSON or model file.")
    allowed={".nojekyll","index.html","published-development-manifest.json"}
    base_runs=("run-02","dev-a","dev-b","dev-c","dev-d","dev-e",
               "dev-f","dev-g","dev-h","dev-f-points","dev-g-points","dev-h-points",
               "dev-i","dev-j","dev-k")
    for run in base_runs:
        allowed.add(f"annoteren/{run}/index.html")
    for png in site.glob("annoteren/*/frame-*.png"):
        require(png.parent.name in (*base_runs,*RUNS),
                "Unapproved prospective or third-party screenshot present.")
        allowed.add(png.relative_to(site).as_posix())
    for run in RUNS:
        allowed.add(f"annoteren/{run}/index.html")
        hashes=manifest["m223Screenshots"][run]
        require(len(hashes)==18,"Missing approved source screenshot hashes.")
        for filename,expected in hashes.items():
            name=f"annoteren/{run}/{filename}"
            require(re.fullmatch(r"frame-\d{4}\.png",filename) and
                    sha(files.get(name,b""))==expected,
                    "Published original new screenshot SHA differs.")
    require(allowed==set(files),"Unexpected site paths: owner labels or prospective assets.")
    if parent is not None:
        original={p.relative_to(parent).as_posix():p.read_bytes()
                  for p in parent.rglob("*") if p.is_file() and not p.is_symlink()}
        require(len(original)==180,"Original M2.21 public checkpoint unexpectedly changed.")
        for name,raw in original.items():
            if name not in ("index.html","published-development-manifest.json"):
                require(files.get(name)==raw,"Previously authorized screenshot/page bytes overwritten.")
    if admitted:
        require(all(files.get(name)==raw for name,raw in admitted.items()),
                "Authentic new original source frame/reviewer differs.")
    digest=tree_hash(files)
    if require_pinned:
        require(digest==PUBLIC_TREE_SHA256,
                "Site does not match independently frozen entire approved public tree SHA256.")
    print("PASS M2.24: 237 exact static files, 216 original PNGs, 18 UNKNOWN reviewers; no source traces, owner labels or sealed prospectives.")
    return digest


def build(parent,archive,target):
    verify_original_site(parent)
    require(not target.exists() or not any(target.iterdir()),"Refuse to overwrite site or original study.")
    extra,hashes=admitted_originals(archive)
    originals={p.relative_to(parent).as_posix():p.read_bytes()
               for p in parent.rglob("*") if p.is_file() and not p.is_symlink()}
    require(len(originals)==180 and
            all(f"annoteren/{r}/index.html" not in originals for r in RUNS),
            "Would overwrite previously published reviewer.")
    for name,raw in {**originals,**extra}.items():
        output=target/name
        output.parent.mkdir(parents=True,exist_ok=True)
        output.write_bytes(raw)
    manifest_file=target/"published-development-manifest.json"
    manifest=json.loads(manifest_file.read_text())
    require("m223Screenshots" not in manifest,"Cannot overwrite existing M2.23 study.")
    manifest.update({"m223PointRoutes":[f"annoteren/{run}/" for run in RUNS],
                     "m223SourceStudySeed":SOURCE_SEED,
                     "m223Screenshots":hashes,
                     "m223ReviewConsent":"standing owner approval for original development screenshots and blank reviewers; never owner labels",
                     "m223HumanTruth":"ALL_UNKNOWN"})
    manifest_file.write_text(json.dumps(manifest,indent=2,sort_keys=True)+"\n")
    home=target/"index.html"
    html=home.read_text()
    require(html.count("</ul>")==1,"Cannot safely add original reviewer links.")
    links="".join(f'<li><a href="annoteren/{run}/">M2.23 {run}: nieuwe ontwikkelreview (18 originele beelden)</a></li>'
                  for run in RUNS)
    home.write_text(html.replace("</ul>",links+"</ul>",1))
    digest=verify(target,parent,extra,require_pinned=False)
    print("NEW_APPROVED_PUBLIC_TREE_SHA256="+digest)


def main():
    p=argparse.ArgumentParser()
    sub=p.add_subparsers(dest="cmd",required=True)
    a=sub.add_parser("build")
    a.add_argument("--parent",required=True,type=Path)
    a.add_argument("--archive",required=True,type=Path)
    a.add_argument("--output",required=True,type=Path)
    a=sub.add_parser("verify")
    a.add_argument("--site",required=True,type=Path)
    args=p.parse_args()
    if args.cmd=="build":
        build(args.parent,args.archive,args.output)
    else:
        print("PUBLIC_TREE_SHA256="+verify(args.site))


if __name__=="__main__":
    main()
