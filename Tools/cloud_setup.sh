#!/usr/bin/env bash
# =============================================================================
# Hexiege 클라우드 환경 전용 설치 스크립트
# -----------------------------------------------------------------------------
# 언제 실행되나?
#   클라우드 세션이 시작될 때, 환경 setup script(저장소 밖에서 관리)가
#   공용 도구(.NET, Unity Hub, p7zip-full, cpio 등)와 Unity 에디터
#   (/opt/unity/<버전>/Editor)를 먼저 설치한 뒤, 저장소 루트에서
#       bash Tools/cloud_setup.sh
#   로 이 스크립트를 실행한다. root 권한 + 비대화형(질문 없이 끝까지 실행)이다.
#
# 무엇을 설치하나? (헥시즈 프로젝트에만 필요한 것들)
#   1) Unity Android Build Support 모듈
#        → Android 빌드를 하려면 에디터 안에 AndroidPlayer 폴더가 있어야 한다.
#   2) Firebase Unity SDK 13.11.0 중 FirebaseAuth 패키지
#        → Firebase는 용량이 커서 .gitignore 대상이다(로컬 임포트 방침).
#          그래서 저장소를 받아도 Assets/Firebase/ 가 없으므로 여기서 채운다.
#   3) GPGS(Google Play Games Plugin 2.1.0)
#        → 2026-11-27 사용자 결정으로 저장소에 직접 커밋되므로 설치할 필요가 없다.
#          (클라우드에서는 GitHub 릴리스 다운로드가 막혀 있고, 실제 크기도 2.2MB로 작다.)
#
# 동작 원칙
#   - 멱등(idempotent): 이미 설치돼 있으면 그 단계는 건너뛴다.
#     → 여러 번 실행해도 결과가 같고, 두 번째부터는 금방 끝난다.
#   - 한 단계가 실패해도 다음 단계는 계속 진행한다.
#     → 대신 실패가 하나라도 있었으면 마지막에 종료코드 1로 끝낸다.
#   - 임시 파일은 mktemp -d 로 만든 폴더에만 두고, 스크립트가 끝나면(성공/실패
#     상관없이) trap 으로 모두 지운다.
#   - 로그는 모두 "[hexiege-setup]" 접두사를 붙여 출력한다.
#
# 선택 환경변수 (테스트/재사용용 — 평소에는 설정하지 않아도 된다)
#   HEXIEGE_REPO_ROOT          저장소 루트 경로. 비우면 이 스크립트가 있는
#                              Tools/ 폴더의 상위 폴더를 쓴다.
#   HEXIEGE_FIREBASE_AUTH_PKG  이미 받아 둔 FirebaseAuth.unitypackage 경로.
#                              지정하면 758MB SDK zip 다운로드를 생략한다.
#
# 다른 스크립트에서 source 로 불러오면 함수만 정의되고 main 은 실행되지 않는다.
# (설치 로직을 저장소 밖 가짜 폴더에서 따로 시험해 보기 위한 장치)
# =============================================================================

# -u: 정의되지 않은 변수를 쓰면 즉시 오류 → 오타로 엉뚱한 경로를 지우는 사고 방지
# pipefail: 파이프 중간 명령이 실패해도 실패로 인식
# (-e 는 쓰지 않는다: 한 단계 실패가 스크립트 전체를 멈추면 안 되기 때문)
set -uo pipefail

# ---------------------------------------------------------------------------
# 공통 설정
# ---------------------------------------------------------------------------

# 로그 접두사
readonly LOG_PREFIX="[hexiege-setup]"

# Unity 에디터가 설치되는 상위 폴더 (환경 setup script 와 약속된 위치)
readonly UNITY_ROOT="/opt/unity"

# Unity 공식 다운로드 서버 주소 (리비전 별로 하위 폴더가 나뉜다)
readonly UNITY_DOWNLOAD_BASE="https://download.unity3d.com/download_unity"

# Firebase Unity SDK 버전과 다운로드 주소
# 근거: Assets/_Project/Docs/AuthSystemRules.md — 기준 SDK 는 Firebase Unity 13.11.0
readonly FIREBASE_SDK_VERSION="13.11.0"
readonly FIREBASE_SDK_URL="https://dl.google.com/firebase/sdk/unity/firebase_unity_sdk_${FIREBASE_SDK_VERSION}.zip"

# 저장소 루트: 환경변수로 지정하지 않았으면 "이 파일이 있는 폴더(Tools)의 상위"
REPO_ROOT="${HEXIEGE_REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

# 이 스크립트가 만든 임시 폴더 목록. 종료 시 trap 이 이 목록을 전부 지운다.
TEMP_DIRS=()

# ---------------------------------------------------------------------------
# 공통 도우미 함수
# ---------------------------------------------------------------------------

# 일반 로그 출력
log() {
    echo "${LOG_PREFIX} $*"
}

# 경고 로그 출력 (표준 오류로 보낸다)
warn() {
    echo "${LOG_PREFIX} 경고: $*" >&2
}

# 임시 폴더를 만들고, 종료 시 자동 삭제 목록에 등록한다.
#   $1 = 임시 폴더를 만들 상위 폴더 (예: /tmp, /opt/unity)
# 만든 폴더 경로는 전역 변수 NEW_TEMP_DIR 에 넣는다.
# (값을 $(...) 로 받으면 서브셸에서 실행되어 TEMP_DIRS 등록이 사라지므로
#  일부러 전역 변수로 돌려준다.)
make_temp_dir() {
    local parent="$1"
    NEW_TEMP_DIR=""
    mkdir -p "$parent" || return 1
    NEW_TEMP_DIR="$(mktemp -d "${parent%/}/hexiege-setup.XXXXXX")" || return 1
    TEMP_DIRS+=("$NEW_TEMP_DIR")
}

# 스크립트 종료 시(정상/오류/중단 모두) 임시 폴더를 모두 지운다.
cleanup_temp_dirs() {
    local dir
    for dir in "${TEMP_DIRS[@]+"${TEMP_DIRS[@]}"}"; do
        # mktemp 가 만든 이름 규칙과 맞는 경로만 지운다 (엉뚱한 경로 삭제 방지)
        if [[ -n "$dir" && -d "$dir" && "$dir" == *"/hexiege-setup."* ]]; then
            rm -rf -- "$dir"
        fi
    done
}

# 파일 다운로드. 실패하면 3번까지 다시 시도한다.
#   $1 = 받을 주소, $2 = 저장할 파일 경로
download_file() {
    local url="$1"
    local out="$2"
    # -f: HTTP 오류(404 등)를 실패로 처리 / -L: 리다이렉트 따라감
    # -sS: 진행 막대는 숨기되 오류 메시지는 출력
    curl -fL -sS --retry 3 --retry-delay 5 -o "$out" "$url"
}

# ---------------------------------------------------------------------------
# 1단계: Unity Android Build Support 모듈
# ---------------------------------------------------------------------------

# ProjectSettings/ProjectVersion.txt 에서 에디터 버전과 리비전을 읽는다.
#   예) "m_EditorVersionWithRevision: 6000.3.5f2 (3fa8bc678cb0)"
#       → UNITY_VERSION=6000.3.5f2, UNITY_REVISION=3fa8bc678cb0
# Windows 에서 저장된 파일일 수 있으므로 줄 끝의 \r 을 먼저 지운다.
parse_project_version() {
    local version_file="${REPO_ROOT}/ProjectSettings/ProjectVersion.txt"
    local line
    UNITY_VERSION=""
    UNITY_REVISION=""

    if [[ ! -f "$version_file" ]]; then
        warn "ProjectVersion.txt 가 없습니다: ${version_file}"
        return 1
    fi

    line="$(grep -m1 '^m_EditorVersionWithRevision:' "$version_file" | tr -d '\r')"
    # 정규식으로 "버전 (리비전)" 부분을 잘라낸다. BASH_REMATCH[1]=버전, [2]=리비전
    if [[ "$line" =~ ^m_EditorVersionWithRevision:[[:space:]]*([^[:space:]]+)[[:space:]]*\(([0-9a-fA-F]+)\) ]]; then
        UNITY_VERSION="${BASH_REMATCH[1]}"
        UNITY_REVISION="${BASH_REMATCH[2]}"
        return 0
    fi

    warn "ProjectVersion.txt 에서 버전/리비전을 읽지 못했습니다: '${line}'"
    return 1
}

# Unity 컴포넌트 목록(.ini)의 [Android] 섹션에서 url= 과 md5= 값을 읽는다.
#   $1 = ini 파일 경로
# 결과는 ANDROID_PKG_URL(상대 경로), ANDROID_PKG_MD5 에 넣는다.
# 주의: "[Android SDK & NDK Tools]" 같은 비슷한 이름의 섹션과 섞이지 않도록
#       섹션 이름이 정확히 "[Android]" 인 곳만 본다.
parse_android_ini() {
    local ini_file="$1"
    ANDROID_PKG_URL="$(tr -d '\r' < "$ini_file" | awk '
        /^\[/ { in_section = ($0 == "[Android]"); next }
        in_section && /^url=/ { sub(/^url=/, ""); print; exit }
    ')"
    ANDROID_PKG_MD5="$(tr -d '\r' < "$ini_file" | awk '
        /^\[/ { in_section = ($0 == "[Android]"); next }
        in_section && /^md5=/ { sub(/^md5=/, ""); print; exit }
    ')"

    if [[ -z "$ANDROID_PKG_URL" || -z "$ANDROID_PKG_MD5" ]]; then
        warn "ini 의 [Android] 섹션에서 url/md5 를 찾지 못했습니다 (url='${ANDROID_PKG_URL}', md5='${ANDROID_PKG_MD5}')"
        return 1
    fi
    return 0
}

# Android Build Support 모듈 설치 본체.
# 반환값: 0 = 성공 또는 건너뜀, 1 = 실패
install_android_module() {
    log "[1/2] Unity Android Build Support 모듈 확인"

    parse_project_version || return 1

    local editor_dir="${UNITY_ROOT}/${UNITY_VERSION}/Editor"
    local target_dir="${editor_dir}/Data/PlaybackEngines/AndroidPlayer"
    log "  프로젝트 Unity 버전: ${UNITY_VERSION} (리비전 ${UNITY_REVISION})"

    # 에디터 자체가 없으면 모듈을 붙일 곳이 없다 → 경고만 하고 건너뜀
    # (에디터 설치는 환경 setup script 의 책임이므로 여기서 실패로 치지 않는다)
    if [[ ! -x "${editor_dir}/Unity" ]]; then
        warn "Unity 에디터가 없습니다(${editor_dir}/Unity) → Android 모듈 설치를 건너뜁니다"
        return 0
    fi

    # 이미 설치돼 있으면 건너뜀 (멱등)
    if [[ -d "$target_dir" ]]; then
        log "  이미 설치됨 → 건너뜀 (${target_dir})"
        return 0
    fi

    # 임시 폴더를 /opt/unity 아래에 만든다.
    # 에디터와 같은 파일시스템이면 마지막 mv 가 복사 없이 즉시 끝난다(6.4GB).
    make_temp_dir "$UNITY_ROOT" || { warn "임시 폴더 생성 실패"; return 1; }
    local work="$NEW_TEMP_DIR"

    # (1) 공식 컴포넌트 목록 ini 를 받아 Android 패키지 주소와 md5 를 알아낸다
    local ini_url="${UNITY_DOWNLOAD_BASE}/${UNITY_REVISION}/unity-${UNITY_VERSION}-linux.ini"
    log "  컴포넌트 목록 다운로드: ${ini_url}"
    download_file "$ini_url" "${work}/components.ini" || { warn "ini 다운로드 실패"; return 1; }
    parse_android_ini "${work}/components.ini" || return 1

    local pkg_url="${UNITY_DOWNLOAD_BASE}/${UNITY_REVISION}/${ANDROID_PKG_URL}"
    local pkg_file="${work}/android.pkg"

    # (2) 패키지(.pkg, 약 2GB) 다운로드
    log "  Android 모듈 다운로드(약 2GB, 시간이 걸립니다): ${pkg_url}"
    download_file "$pkg_url" "$pkg_file" || { warn "Android 모듈 다운로드 실패"; return 1; }

    # (3) md5 검증 — 다운로드가 깨졌거나 다른 파일이면 설치하지 않는다
    local actual_md5
    actual_md5="$(md5sum "$pkg_file" | awk '{print $1}')"
    if [[ "${actual_md5,,}" != "${ANDROID_PKG_MD5,,}" ]]; then
        warn "md5 불일치 (기대 ${ANDROID_PKG_MD5}, 실제 ${actual_md5}) → 설치 중단"
        return 1
    fi
    log "  md5 확인 완료 (${actual_md5})"

    # (4) 압축 해제 1차: .pkg(xar 형식)를 7z 로 풀면 최상위에 "Payload~" 가 생긴다.
    #     Payload~ 는 ASCII cpio(odc) 아카이브이며 약 6.8GB 이다.
    local xar_dir="${work}/xar"
    mkdir -p "$xar_dir"
    log "  .pkg 압축 해제(7z)"
    if ! (cd "$xar_dir" && 7z x -y "$pkg_file" > /dev/null); then
        warn "7z 압축 해제 실패"
        return 1
    fi
    # 디스크 절약: 2GB 원본은 더 이상 필요 없으므로 바로 지운다
    rm -f -- "$pkg_file"

    local payload="${xar_dir}/Payload~"
    if [[ ! -f "$payload" ]]; then
        warn ".pkg 안에서 Payload~ 를 찾지 못했습니다"
        return 1
    fi

    # (5) 압축 해제 2차: cpio 로 풀면 그 폴더의 최상위가 곧 AndroidPlayer 내용이다.
    #     -i: 풀기, -d: 하위 폴더 자동 생성, -m: 파일 수정 시각 유지
    local player_dir="${work}/AndroidPlayer"
    mkdir -p "$player_dir"
    log "  Payload~ 압축 해제(cpio, 약 6.4GB)"
    if ! (cd "$player_dir" && cpio -idm --quiet < "$payload"); then
        warn "cpio 압축 해제 실패"
        return 1
    fi
    # 디스크 절약: 6.8GB Payload~ 도 바로 지운다
    rm -f -- "$payload"

    # 결과가 정말 AndroidPlayer 인지 대표 파일로 간단히 확인
    if [[ ! -f "${player_dir}/UnityEditor.Android.Extensions.dll" ]]; then
        warn "풀린 결과에 UnityEditor.Android.Extensions.dll 이 없습니다 → 설치 중단"
        return 1
    fi

    # (6) 에디터의 PlaybackEngines 폴더로 이동
    mkdir -p "$(dirname "$target_dir")" || { warn "PlaybackEngines 폴더 생성 실패"; return 1; }
    if ! mv -- "$player_dir" "$target_dir"; then
        warn "AndroidPlayer 이동 실패"
        # 이동이 중간에 실패하면 반쯤 복사된 폴더가 남아 다음 실행이 "이미 설치됨"으로
        # 착각할 수 있으므로 지운다
        rm -rf -- "$target_dir"
        return 1
    fi

    log "  설치 완료 → ${target_dir}"
    return 0
}

# ---------------------------------------------------------------------------
# 2단계: Firebase Unity SDK — FirebaseAuth 패키지만
# ---------------------------------------------------------------------------
# 프로젝트 코드는 Firebase, Firebase.Auth, Firebase.Extensions 네임스페이스만 쓴다.
# 이 셋은 모두 FirebaseAuth.unitypackage 하나에 들어 있다(App/Platform 포함).
# 이 단계는 Unity 에디터나 라이선스 없이 동작해야 한다(로그인 전 setup 단계이므로).
# 그래서 Unity 의 -importPackage 대신, .unitypackage 를 직접 풀어 같은 결과를 만든다.

# .unitypackage 를 프로젝트 폴더에 직접 풀어 넣는다.
#   $1 = .unitypackage 경로, $2 = 프로젝트 루트(Assets/ 의 상위)
#
# .unitypackage 의 정체는 tar.gz 이다. 풀면 GUID 이름의 폴더가 여럿 나오고,
# 각 폴더 안에는 다음 파일이 있다.
#   pathname    : 첫 줄 = 프로젝트 기준 경로 (예: Assets/Firebase/Plugins/Firebase.Auth.dll)
#   asset       : 실제 파일 내용 (폴더 항목이면 이 파일이 없다)
#   asset.meta  : Unity 의 .meta 파일 내용 (GUID 등이 들어 있음)
extract_unitypackage() {
    local package_file="$1"
    local project_root="$2"
    local failures=0
    local count=0

    make_temp_dir "${TMPDIR:-/tmp}" || { warn "임시 폴더 생성 실패"; return 1; }
    local unpack_dir="$NEW_TEMP_DIR/unitypackage"
    mkdir -p "$unpack_dir"

    if ! tar -xzf "$package_file" -C "$unpack_dir"; then
        warn ".unitypackage 압축 해제 실패: ${package_file}"
        return 1
    fi

    local entry rel_path dest
    for entry in "$unpack_dir"/*/; do
        entry="${entry%/}"
        [[ -f "${entry}/pathname" ]] || continue

        # 첫 줄만 읽고 \r 제거 (Windows 에서 만든 패키지 대비)
        rel_path="$(head -n 1 "${entry}/pathname" | tr -d '\r')"

        # 안전 검사: 절대경로이거나 ".." 이 들어 있으면 프로젝트 밖에 파일을 쓸 수
        # 있으므로 건너뛴다 (악성/손상 패키지 대비)
        if [[ -z "$rel_path" || "$rel_path" == /* || "$rel_path" == *..* ]]; then
            warn "위험하거나 빈 경로라 건너뜀: '${rel_path}'"
            continue
        fi

        dest="${project_root}/${rel_path}"
        if [[ -f "${entry}/asset" ]]; then
            # 파일 항목: 상위 폴더를 만들고 내용을 복사
            if ! { mkdir -p "$(dirname "$dest")" && cp -f -- "${entry}/asset" "$dest"; }; then
                warn "파일 복사 실패: ${rel_path}"
                failures=$((failures + 1))
                continue
            fi
        else
            # 폴더 항목: 폴더만 만든다
            if ! mkdir -p "$dest"; then
                warn "폴더 생성 실패: ${rel_path}"
                failures=$((failures + 1))
                continue
            fi
        fi

        # .meta 가 있으면 "<경로>.meta" 로 복사 (GUID 를 원본과 같게 유지해야
        # 다른 에셋의 참조가 깨지지 않는다)
        if [[ -f "${entry}/asset.meta" ]]; then
            if ! cp -f -- "${entry}/asset.meta" "${dest}.meta"; then
                warn ".meta 복사 실패: ${rel_path}.meta"
                failures=$((failures + 1))
                continue
            fi
        fi
        count=$((count + 1))
    done

    log "  항목 ${count}개 설치 (실패 ${failures}개)"
    # 다음 실행을 위해 큰 임시 폴더는 바로 지운다
    rm -rf -- "$unpack_dir"
    [[ "$failures" -eq 0 && "$count" -gt 0 ]]
}

# Firebase Auth 설치 본체.
# 반환값: 0 = 성공 또는 건너뜀, 1 = 실패
install_firebase_auth() {
    log "[2/2] Firebase Unity SDK ${FIREBASE_SDK_VERSION} (FirebaseAuth) 확인"

    local marker="${REPO_ROOT}/Assets/Firebase/Plugins/Firebase.Auth.dll"
    if [[ -f "$marker" ]]; then
        log "  이미 설치됨 → 건너뜀 (${marker})"
        return 0
    fi

    if [[ ! -d "${REPO_ROOT}/Assets" ]]; then
        warn "Assets 폴더가 없습니다: ${REPO_ROOT}/Assets"
        return 1
    fi

    local package_file
    if [[ -n "${HEXIEGE_FIREBASE_AUTH_PKG:-}" ]]; then
        # 이미 받아 둔 패키지를 재사용 (758MB 다운로드 생략)
        package_file="$HEXIEGE_FIREBASE_AUTH_PKG"
        if [[ ! -f "$package_file" ]]; then
            warn "HEXIEGE_FIREBASE_AUTH_PKG 파일이 없습니다: ${package_file}"
            return 1
        fi
        log "  기존 패키지 사용: ${package_file}"
    else
        make_temp_dir "${TMPDIR:-/tmp}" || { warn "임시 폴더 생성 실패"; return 1; }
        local work="$NEW_TEMP_DIR"
        local zip_file="${work}/firebase_unity_sdk.zip"

        log "  SDK 다운로드(약 758MB): ${FIREBASE_SDK_URL}"
        download_file "$FIREBASE_SDK_URL" "$zip_file" || { warn "Firebase SDK 다운로드 실패"; return 1; }

        # zip 안에서 FirebaseAuth.unitypackage 하나만 꺼낸다
        # -o: 덮어쓰기 묻지 않음, -j: zip 안의 폴더 구조를 버리고 파일만 꺼냄
        if ! unzip -o -j -q "$zip_file" '*FirebaseAuth.unitypackage' -d "$work"; then
            warn "zip 에서 FirebaseAuth.unitypackage 추출 실패"
            return 1
        fi
        # 디스크 절약: 758MB zip 은 바로 지운다
        rm -f -- "$zip_file"

        package_file="${work}/FirebaseAuth.unitypackage"
        if [[ ! -f "$package_file" ]]; then
            warn "추출 결과에 FirebaseAuth.unitypackage 가 없습니다"
            return 1
        fi
    fi

    extract_unitypackage "$package_file" "$REPO_ROOT" || { warn "FirebaseAuth 설치 중 실패가 있었습니다"; return 1; }

    if [[ ! -f "$marker" ]]; then
        warn "설치 후에도 ${marker} 가 없습니다"
        return 1
    fi
    log "  설치 완료 → ${REPO_ROOT}/Assets/Firebase"
    return 0
}

# ---------------------------------------------------------------------------
# GPGS (Google Play Games Plugin 2.1.0)
# ---------------------------------------------------------------------------
# 설치 단계 없음. 2026-11-27 사용자 결정으로 Assets/GooglePlayGames/ 와
# Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/ 가 저장소에 커밋된다.

# ---------------------------------------------------------------------------
# 진입점
# ---------------------------------------------------------------------------
main() {
    # 어떤 이유로 끝나든 임시 폴더를 지운다
    trap cleanup_temp_dirs EXIT

    local failed=0
    log "시작 (저장소 루트: ${REPO_ROOT})"

    # 각 단계는 독립적으로 실행한다. 앞 단계가 실패해도 다음 단계는 진행.
    install_android_module || { warn "Android 모듈 단계 실패"; failed=1; }
    install_firebase_auth  || { warn "Firebase Auth 단계 실패"; failed=1; }
    log "GPGS 는 저장소에 포함되어 있어 설치하지 않습니다"

    if [[ "$failed" -ne 0 ]]; then
        warn "실패한 단계가 있습니다 (위 로그 참고)"
        return 1
    fi
    log "모든 단계 완료"
    return 0
}

# 직접 실행했을 때만 main 을 부른다. (source 로 불러오면 함수 정의만 한다)
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    main "$@"
    exit $?
fi
