#!/usr/bin/env python3
"""
Build-time NPC dialogue generator using local EXAONE 3.5 via Ollama.

Reads role + personality + context from a config table, generates N lines per
slot, dedupes, and writes the result to Assets/Resources/Dialogue/dialogue_lines.json
which CastleViewUI / NPCInteractionUI / NPCManager can load at runtime.

Run: python generate.py [--lines N] [--role vassal,soldier] [--lang ko,en]

Requirements: ollama running locally with model `exaone3.5:7.8b` pulled.
"""
from __future__ import annotations
import argparse, json, os, sys, time, hashlib
import urllib.request, urllib.error

OLLAMA_URL = "http://localhost:11434/api/generate"
MODEL      = "exaone3.5:7.8b"
OUT_PATH   = os.path.normpath(os.path.join(
    os.path.dirname(__file__), "..", "..",
    "Assets", "Resources", "Dialogue", "dialogue_lines.json"))

# (role_id, korean_role_name, english_role_name, personality_korean, background_korean)
ROLES = [
    ("vassal",   "가신",  "vassal",   "충성스럽고 현명한 노년의 청지기",
                 "20년간 성을 섬겨온 베테랑 청지기. 실용적이고 지혜롭다."),
    ("soldier",  "병사",  "soldier",  "용감하지만 무모한 젊은 전사",
                 "전투와 영광을 갈망하는 젊은 병사. 무모하지만 두려움이 없다."),
    ("farmer",   "농부",  "farmer",   "성실하고 정직한 중년의 농부",
                 "평생 이 땅에서 농사를 지어온 단단한 농부. 정직하고 믿을 만하다."),
    ("merchant", "상인",  "merchant", "약삭빠르고 욕심 많은 떠돌이 상인",
                 "성에 정착한 떠돌이 상인. 영악하고 항상 이익을 좇는다."),
]

# (context_id, ko_description, count_per_role)
CONTEXTS = [
    ("greeting",   "영주를 처음 만나거나 인사할 때 하는 말",                         50),
    ("idle",       "특별한 일이 없을 때 혼잣말이나 푸념처럼 중얼거리는 말",          80),
    ("accept",     "영주가 임무를 맡겼을 때 흔쾌히 받아들이는 말",                    30),
    ("refuse",     "영주를 못 미덥게 여겨 임무를 거절하거나 불평하는 말 (충성도 낮음)", 30),
    ("good_news",  "곡식이 풍년이거나 금고가 가득 찬 좋은 소식을 들었을 때 반응",     30),
    ("bad_news",   "오크 침략, 화재, 기근 같은 나쁜 소식을 들었을 때 반응",         30),
]
# Per role: 50+80+30+30+30+30 = 250 lines × 4 roles = 1000 lines total

PROMPT_TEMPLATE = """당신은 중세 판타지 왕국 시뮬레이션 게임 'Little Lord Majesty'의 대사 작가입니다.
영주를 모시는 NPC가 다양한 상황에서 할 만한 짧은 한국어 대사를 만듭니다.

캐릭터 직업: {role_ko} ({role_en})
캐릭터 성격: {personality_ko}
배경: {background_ko}

상황: {context_desc}

위 캐릭터가 위 상황에서 할 만한 짧은 대사를 정확히 {n}줄 만들어 주세요.

엄격한 규칙:
- 한 줄당 한 대사 (10~30자 정도, 너무 길지 않게)
- 각 줄 앞에 번호나 기호 붙이지 말 것 (그냥 대사만)
- 따옴표 (\"...\") 붙이지 말 것
- 이모지·이모티콘·특수문자 사용 금지
- 캐릭터 성격과 직업이 드러나야 함
- 같은 표현을 반복하지 말고 다양하게
- 영주를 부를 때는 "영주님", "주군" 또는 그 캐릭터다운 호칭 사용
- {n}줄을 다 쓰면 멈출 것 (그 외의 설명/사족 금지)

이제 정확히 {n}줄을 출력하세요:"""


def gen_lines(role_id, role_ko, role_en, personality_ko, background_ko,
              context_id, context_desc, n) -> list[str]:
    prompt = PROMPT_TEMPLATE.format(
        role_ko=role_ko, role_en=role_en,
        personality_ko=personality_ko,
        background_ko=background_ko,
        context_desc=context_desc, n=n)
    body = {
        "model": MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {
            "temperature": 0.85,
            "top_p":       0.92,
            "num_predict": n * 50,   # generous ceiling
            "stop":        ["\n\n\n"],
        },
    }
    req = urllib.request.Request(
        OLLAMA_URL,
        data=json.dumps(body, ensure_ascii=False).encode("utf-8"),
        headers={"Content-Type": "application/json; charset=utf-8"})
    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            data = json.loads(resp.read().decode("utf-8"))
    except urllib.error.URLError as e:
        print(f"[ERR] ollama request failed for {role_id}/{context_id}: {e}",
              file=sys.stderr)
        return []
    text = data.get("response", "").strip()
    return _clean(text)


def _clean(text: str) -> list[str]:
    out = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            continue
        # strip leading numeric markers like "1.", "1)", "- ", "• " etc.
        for prefix in [")", "."]:
            if len(line) > 2 and line[0].isdigit():
                # eat digits then prefix
                i = 0
                while i < len(line) and line[i].isdigit():
                    i += 1
                if i < len(line) and line[i] in ").":
                    line = line[i + 1:].strip()
                    break
        if line.startswith(("- ", "* ", "• ", "▪ ", "·")):
            line = line[2:].strip()
        # strip surrounding quotes
        if len(line) >= 2 and line[0] in "\"'“‘『「" and line[-1] in "\"'”’』」":
            line = line[1:-1].strip()
        if not line:
            continue
        # reject obvious junk: too short, too long, contains LLM disclaimers
        if len(line) < 4 or len(line) > 80:
            continue
        if any(bad in line for bad in
               ("죄송", "AI", "모델", "여기 ", "다음은", "생성", "출력")):
            continue
        out.append(line)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT_PATH)
    ap.add_argument("--retries", type=int, default=2,
                    help="extra rounds if a slot has fewer lines than target")
    ap.add_argument("--only-role",
                    help="comma-separated role_ids to regenerate (e.g. vassal,soldier)")
    args = ap.parse_args()

    target_roles = ROLES
    if args.only_role:
        wanted = set(args.only_role.split(","))
        target_roles = [r for r in ROLES if r[0] in wanted]

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    # load existing so a partial regen doesn't blow away other slots
    if os.path.exists(args.out):
        with open(args.out, "r", encoding="utf-8") as f:
            result = json.load(f)
    else:
        result = {}

    total_target = sum(c[2] for c in CONTEXTS) * len(target_roles)
    print(f"[gen] Generating ~{total_target} lines via {MODEL}")
    total_started = time.time()

    for role_id, role_ko, role_en, personality_ko, background_ko in target_roles:
        result.setdefault(role_id, {})
        for ctx_id, ctx_desc, n in CONTEXTS:
            slot = set()
            attempts = 0
            t0 = time.time()
            while len(slot) < n and attempts <= args.retries:
                want = n - len(slot)
                lines = gen_lines(role_id, role_ko, role_en, personality_ko,
                                  background_ko, ctx_id, ctx_desc, want)
                for ln in lines:
                    slot.add(ln)
                attempts += 1
            collected = sorted(slot)[:n]
            result[role_id][ctx_id] = collected
            dt = time.time() - t0
            print(f"[gen] {role_id:9s}/{ctx_id:9s}: {len(collected):3d}/{n} "
                  f"in {dt:5.1f}s "
                  f"({attempts} attempt{'s' if attempts != 1 else ''})")
            # checkpoint after each slot so a crash doesn't lose work
            with open(args.out, "w", encoding="utf-8") as f:
                json.dump(result, f, ensure_ascii=False, indent=2)

    total_dt = time.time() - total_started
    grand_total = sum(len(s) for r in result.values() for s in r.values())
    print(f"[gen] DONE - {grand_total} unique lines, {total_dt/60:.1f} min")
    print(f"[gen] wrote -> {args.out}")


if __name__ == "__main__":
    sys.exit(main())
