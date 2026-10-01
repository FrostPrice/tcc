#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Uso:
  bash SplatVRLabUnity/scripts/collect_quest_native_orbit.sh \
    --adb "$QUEST_ADB" \
    --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-orbit-baseline-fullcircle-dev-v01.apk \
    --run-id quest_orbit_baseline_fullcircle_r01

Para APKs 100k, 50k e splatfacto-big, informe também --expected-variant e
--expected-representation conforme a variante do build.

Coleta 144 poses do APK orbital nativo. A aplicação só avança quando o host
captura a tela, valida o índice da pose e envia a confirmação por ADB.
Não mede conforto de locomoção e não substitui a avaliação estacionária.
EOF
}

ADB=adb
APK=
RUN_ID=
OUTPUT_DIR=
SKIP_INSTALL=false
EXPECTED_VARIANT=spark_baseline_orbit_full_circle_v01
EXPECTED_REPRESENTATION=baseline_v01
REMOTE_BASE=/sdcard/Android/data/br.edu.univali.splatvrlab/files/visual_evaluation
REMOTE_METRICS=/sdcard/Android/data/br.edu.univali.splatvrlab/files/measurements

while (($#)); do
    case "$1" in
        --adb) ADB=$2; shift 2 ;;
        --apk) APK=$2; shift 2 ;;
        --run-id) RUN_ID=$2; shift 2 ;;
        --output-dir) OUTPUT_DIR=$2; shift 2 ;;
        --skip-install) SKIP_INSTALL=true; shift ;;
        --expected-variant) EXPECTED_VARIANT=$2; shift 2 ;;
        --expected-representation) EXPECTED_REPRESENTATION=$2; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Opção desconhecida: $1" >&2; usage >&2; exit 2 ;;
    esac
done

[[ -f "$APK" ]] || { echo "APK não encontrado: $APK" >&2; exit 2; }
[[ "$RUN_ID" =~ ^[A-Za-z0-9_-]+$ ]] || {
    echo "--run-id inválido: $RUN_ID" >&2; exit 2;
}
[[ "$EXPECTED_VARIANT" =~ ^[A-Za-z0-9_]+$ &&
   "$EXPECTED_REPRESENTATION" =~ ^[A-Za-z0-9_]+$ ]] || {
    echo "Identificadores esperados inválidos." >&2; exit 2;
}
command -v "$ADB" >/dev/null || { echo "ADB não encontrado: $ADB" >&2; exit 2; }
command -v jq >/dev/null || { echo "jq não encontrado" >&2; exit 2; }
command -v rg >/dev/null || { echo "ripgrep não encontrado" >&2; exit 2; }
OUTPUT_DIR=${OUTPUT_DIR:-experiments/unitysplats_viability_v01/evidence/$RUN_ID}
[[ ! -e "$OUTPUT_DIR" ]] || {
    echo "Saída já existe; não será sobrescrita: $OUTPUT_DIR" >&2; exit 2;
}
mkdir -p "$OUTPUT_DIR/poses" "$OUTPUT_DIR/screenshots"
APK_SHA=$(sha256sum "$APK" | awk '{print $1}')
printf '%s\n' "$APK_SHA  $APK" > "$OUTPUT_DIR/apk_sha256.txt"

adb_call() { "$ADB" "$@"; }
remote_runs() {
    adb_call shell ls -1 "$REMOTE_BASE" 2>/dev/null |
        tr -d '\r' | rg '^orbit_full_circle_[0-9]{8}T[0-9]{9}Z$' | sort || true
}
remote_metrics() {
    adb_call shell ls -1 "$REMOTE_METRICS" 2>/dev/null | tr -d '\r' |
        rg "^unitysplats_viability_v01_${EXPECTED_VARIANT}_.*\\.json$" |
        sort || true
}
before=$(remote_runs)
before_metrics=$(remote_metrics)
if [[ "$SKIP_INSTALL" == false ]]; then
    adb_call install -r "$APK" | tee "$OUTPUT_DIR/install.log"
fi
adb_call shell am force-stop br.edu.univali.splatvrlab
adb_call shell monkey -p br.edu.univali.splatvrlab \
    -c android.intent.category.LAUNCHER 1 | tee "$OUTPUT_DIR/launch.log"
echo "Mantenha o headset rastreado e imóvel; a cena avançará em passos de 2,5°."
echo "Interrompa a sessão se houver desconforto. Pode levar vários minutos."

remote_run=
for ((attempt=0; attempt<120; attempt++)); do
    after=$(remote_runs)
    new=$(comm -13 <(printf '%s\n' "$before" | sed '/^$/d') \
        <(printf '%s\n' "$after" | sed '/^$/d') | tail -n 1)
    if [[ -n "$new" ]]; then
        remote_run=$REMOTE_BASE/$new
        break
    fi
    sleep 1
done
[[ -n "$remote_run" ]] || {
    adb_call logcat -d -s Unity:I > "$OUTPUT_DIR/unity_logcat.txt" || true
    echo "A aplicação não criou um diretório orbital novo; veja unity_logcat.txt" >&2
    exit 1
}
printf '%s\n' "$remote_run" > "$OUTPUT_DIR/remote_run_path.txt"

ack_file=$(mktemp)
trap 'rm -f "$ack_file"' EXIT
printf 'screenshot_captured\n' > "$ack_file"
adb_call push "$ack_file" "$remote_run/.adb_write_probe" >/dev/null || {
    echo "ADB não consegue escrever no diretório externo do aplicativo." >&2
    exit 1
}
adb_call shell test -f "$remote_run/.adb_write_probe" || {
    echo "O aplicativo não recebeu o arquivo de sincronização por ADB." >&2
    exit 1
}

for ((index=0; index<144; index++)); do
    stem=$(printf 'pose_%04d' "$index")
    ready=$remote_run/$stem.ready.json
    found=false
    for ((attempt=0; attempt<120; attempt++)); do
        if adb_call shell test -f "$ready"; then found=true; break; fi
        sleep 1
    done
    [[ "$found" == true ]] || {
        echo "Timeout esperando $ready; evidência parcial preservada." >&2
        exit 1
    }
    adb_call pull "$ready" "$OUTPUT_DIR/poses/$stem.json" >/dev/null
    jq -e --argjson index "$index" --arg variant "$EXPECTED_VARIANT" \
        --arg representation "$EXPECTED_REPRESENTATION" '
        .frameIndex == $index and .frameCount == 144 and
        .trajectoryId == "chair_orbit_full_circle_r125_v01" and
        .variantId == $variant and .representationVariantId == $representation and
        ((.angleDegrees - ($index * 2.5)) | fabs) < 0.01
    ' "$OUTPUT_DIR/poses/$stem.json" >/dev/null || {
        echo "Marcador da pose $index não corresponde à sequência esperada." >&2
        exit 1
    }
    screenshot=$OUTPUT_DIR/screenshots/$stem.png
    adb_call exec-out screencap -p > "$screenshot"
    [[ $(wc -c < "$screenshot") -gt 10000 ]] || {
        echo "Screenshot $stem é pequeno demais; pose não confirmada." >&2
        exit 1
    }
    [[ $(od -An -tx1 -N8 "$screenshot" | tr -d ' \n') == 89504e470d0a1a0a ]] || {
        echo "Screenshot $stem não é PNG; pose não confirmada." >&2
        exit 1
    }
    adb_call push "$ack_file" "$remote_run/$stem.ack" >/dev/null
    adb_call shell test -f "$remote_run/$stem.ack"
    printf 'Pose %03d/144: %.1f°\n' "$((index+1))" "$(jq -r .angleDegrees "$OUTPUT_DIR/poses/$stem.json")"
done

completion=$remote_run/completion.json
for ((attempt=0; attempt<30; attempt++)); do
    if adb_call shell test -f "$completion"; then break; fi
    sleep 1
done
adb_call pull "$completion" "$OUTPUT_DIR/completion.json" >/dev/null
jq -e '.status == "complete" and .acknowledgedFrames == 144 and .expectedFrames == 144' \
    "$OUTPUT_DIR/completion.json" >/dev/null || {
        echo "A aplicação não confirmou a conclusão de 144 poses." >&2; exit 1;
    }

# completion.json is written immediately before FrameMetricsRecorder flushes its
# report. Wait for a *new, complete* JSON instead of treating this race as a
# failed 144-pose capture. A partially written JSON is kept as .pending.
metric=
for ((attempt=0; attempt<60; attempt++)); do
    after_metrics=$(remote_metrics)
    metric=$(comm -13 <(printf '%s\n' "$before_metrics" | sed '/^$/d') \
        <(printf '%s\n' "$after_metrics" | sed '/^$/d') | tail -n 1)
    if [[ -n "$metric" ]]; then
        pending="$OUTPUT_DIR/$metric.pending"
        if adb_call pull "$REMOTE_METRICS/$metric" "$pending" >/dev/null 2>&1 &&
            jq -e --arg variant "$EXPECTED_VARIANT" \
                --arg representation "$EXPECTED_REPRESENTATION" '
                .variantId == $variant and
                .representationVariantId == $representation and
                .conditionId == "orbit_full_circle_capture" and
                .completionReason == "native_orbit_capture_completed"
            ' "$pending" >/dev/null 2>&1; then
            mv "$pending" "$OUTPUT_DIR/$metric"
            break
        fi
    fi
    sleep 1
done
if [[ -z "$metric" || ! -f "$OUTPUT_DIR/$metric" ]]; then
    adb_call logcat -d -s Unity:I > "$OUTPUT_DIR/unity_logcat.txt" || true
    echo "Relatório válido não apareceu em 60 s; capturas e conclusão foram preservadas." >&2
    exit 1
fi
adb_call logcat -d -s Unity:I > "$OUTPUT_DIR/unity_logcat.txt" || true
echo "144 capturas preservadas em: $OUTPUT_DIR"
echo "Verifique visualmente os PNGs: o PNG válido pode ainda conter tela preta ou sobreposição."
