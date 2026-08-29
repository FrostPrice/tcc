#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Uso:
  bash SplatVRLabUnity/scripts/collect_quest_metrics_run.sh \
    --adb /caminho/para/adb \
    --apk SplatVRLabUnity/Builds/Android/SplatVRLabUnity-locomotion-dev.apk \
    --condition static_reference \
    --run-id quest_metrics_v01_static_r01

Opções:
  --adb PATH                 executável adb (padrão: adb)
  --apk PATH                 APK que será identificado e, por padrão, instalado
  --condition NAME           static_reference, continuous_walk ou snap_turn
  --run-id NAME              identificador único da repetição (sem / ou espaços)
  --output-dir PATH          diretório de evidência; padrão: experiments/.../evidence/RUN_ID
  --capture-seconds N        duração externa da sessão; padrão: 45
  --skip-install             não reinstala o APK

Antes de iniciar, ative a gravação CSV no OVR Metrics Tool. O script não remove
nenhuma medição do headset. O JSON interno registra a hora exata em que a janela
de 30 segundos começou, após os 180 frames de aquecimento.
EOF
}

ADB="adb"
APK=""
CONDITION=""
RUN_ID=""
OUTPUT_DIR=""
CAPTURE_SECONDS=45
MAX_REPORT_WAIT_SECONDS=60
LAUNCH_CHECK_SECONDS=5
INSTALL_APK=true

while (($# > 0)); do
    case "$1" in
        --adb) ADB="$2"; shift 2 ;;
        --apk) APK="$2"; shift 2 ;;
        --condition) CONDITION="$2"; shift 2 ;;
        --run-id) RUN_ID="$2"; shift 2 ;;
        --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
        --capture-seconds) CAPTURE_SECONDS="$2"; shift 2 ;;
        --skip-install) INSTALL_APK=false; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Opção desconhecida: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$APK" || -z "$CONDITION" || -z "$RUN_ID" ]]; then
    echo "--apk, --condition e --run-id são obrigatórios." >&2
    usage >&2
    exit 2
fi
if [[ ! -f "$APK" ]]; then
    echo "APK não encontrado: $APK" >&2
    exit 2
fi
if [[ ! "$CONDITION" =~ ^(static_reference|continuous_walk|snap_turn)$ ]]; then
    echo "Condição inválida: $CONDITION" >&2
    exit 2
fi
if [[ ! "$RUN_ID" =~ ^[A-Za-z0-9._-]+$ ]]; then
    echo "--run-id aceita apenas letras, números, ponto, sublinhado e hífen." >&2
    exit 2
fi
if [[ ! "$CAPTURE_SECONDS" =~ ^[0-9]+$ ]] || (( CAPTURE_SECONDS < 40 )); then
    echo "--capture-seconds deve ser inteiro de ao menos 40 segundos." >&2
    exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_DIR="$(cd "$PROJECT_DIR/.." && pwd)"
if [[ -z "$OUTPUT_DIR" ]]; then
    OUTPUT_DIR="$REPO_DIR/experiments/unitysplats_viability_v01/evidence/$RUN_ID"
fi
if [[ -e "$OUTPUT_DIR" ]]; then
    echo "O diretório de saída já existe: $OUTPUT_DIR" >&2
    exit 2
fi

mkdir -p "$OUTPUT_DIR"
APK_SHA256="$(sha256sum "$APK" | awk '{print $1}')"
APK_BYTES="$(wc -c < "$APK" | tr -d '[:space:]')"
HOST_STARTED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
APP_METRICS_DIR="/sdcard/Android/data/br.edu.univali.splatvrlab/files/measurements"
OVR_METRICS_DIR="/sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics"

"$ADB" get-state | grep -qx "device"
if [[ "$INSTALL_APK" == true ]]; then
    "$ADB" install -r "$APK" | tee "$OUTPUT_DIR/install.txt"
else
    printf 'Instalação ignorada por --skip-install\n' > "$OUTPUT_DIR/install.txt"
fi

{ "$ADB" shell ls -1 "$APP_METRICS_DIR" 2>/dev/null || true; } | tr -d '\r' | sort \
    > "$OUTPUT_DIR/app_measurements_before.txt"
{ "$ADB" shell ls -1 "$OVR_METRICS_DIR" 2>/dev/null || true; } | tr -d '\r' | sort \
    > "$OUTPUT_DIR/ovr_metrics_before.txt"
"$ADB" logcat -c
"$ADB" shell monkey -p br.edu.univali.splatvrlab -c android.intent.category.LAUNCHER 1 \
    | tee "$OUTPUT_DIR/launch.txt"
sleep "$LAUNCH_CHECK_SECONDS"
"$ADB" logcat -d -v threadtime > "$OUTPUT_DIR/launch_logcat.txt" || true
if grep -Fq "Launch is blocked because a Reprojected OS dialog is currently showing" \
    "$OUTPUT_DIR/launch_logcat.txt"; then
    printf '%s\n' \
        'A abertura foi bloqueada por um diálogo do sistema do Quest. Desbloqueie o headset e feche o diálogo antes de tentar novamente.' \
        > "$OUTPUT_DIR/launch_blocked.txt"
    printf '%s\n' \
        'ERRO: o Quest bloqueou a abertura por um diálogo do sistema; esta execução não é válida.' >&2
    exit 3
fi

case "$CONDITION" in
    static_reference)
        INSTRUCTION="Permaneça parado na pose inicial, sem tocar nos controles, até o término da captura."
        ;;
    continuous_walk)
        INSTRUCTION="Movimente-se continuamente com o joystick esquerdo, sem giro deliberado, até o término da captura."
        ;;
    snap_turn)
        INSTRUCTION="Permaneça aproximadamente na posição inicial e execute giros de 30 graus com o joystick direito até o término da captura."
        ;;
esac
printf '%s\n' "$INSTRUCTION" | tee "$OUTPUT_DIR/operator_instruction.txt"
printf 'A sessão externa dura %s s. A janela interna exata está no JSON do aplicativo.\n' "$CAPTURE_SECONDS"
sleep "$CAPTURE_SECONDS"

REPORT_WAITED_SECONDS=0
while true; do
    { "$ADB" shell ls -1 "$APP_METRICS_DIR" 2>/dev/null || true; } | tr -d '\r' | sort \
        > "$OUTPUT_DIR/app_measurements_after.txt"
    comm -13 "$OUTPUT_DIR/app_measurements_before.txt" "$OUTPUT_DIR/app_measurements_after.txt" \
        > "$OUTPUT_DIR/app_measurements_new.txt"
    if [[ -s "$OUTPUT_DIR/app_measurements_new.txt" ]]; then
        break
    fi
    if (( REPORT_WAITED_SECONDS >= MAX_REPORT_WAIT_SECONDS )); then
        break
    fi
    printf 'Aguardando o JSON de métricas do aplicativo (%s/%s s adicionais)...\n' \
        "$REPORT_WAITED_SECONDS" "$MAX_REPORT_WAIT_SECONDS"
    sleep 2
    REPORT_WAITED_SECONDS=$((REPORT_WAITED_SECONDS + 2))
done

"$ADB" exec-out screencap -p > "$OUTPUT_DIR/screenshot.png" || true
"$ADB" shell am force-stop br.edu.univali.splatvrlab
"$ADB" logcat -d -v threadtime -s Unity AndroidRuntime > "$OUTPUT_DIR/logcat.txt" || true
mkdir -p "$OUTPUT_DIR/app_measurements"
APPLICATION_REPORT_COMPLETE=true
if [[ -s "$OUTPUT_DIR/app_measurements_new.txt" ]]; then
    while IFS= read -r metric_file; do
        "$ADB" pull "$APP_METRICS_DIR/$metric_file" "$OUTPUT_DIR/app_measurements/$metric_file"
    done < "$OUTPUT_DIR/app_measurements_new.txt"
    if ! grep -Fq '"completionReason": "measurement_window_completed"' \
        "$OUTPUT_DIR"/app_measurements/*.json; then
        APPLICATION_REPORT_COMPLETE=false
        printf '%s\n' \
            'O aplicativo foi pausado ou encerrado antes de concluir a janela de medição. Esta execução não é uma repetição válida.' \
            > "$OUTPUT_DIR/app_measurements_status.txt"
    fi
else
    APPLICATION_REPORT_COMPLETE=false
    printf 'Nenhum JSON novo foi criado após %s s adicionais de espera. Esta execução não é uma repetição válida.\n' "$REPORT_WAITED_SECONDS" \
        > "$OUTPUT_DIR/app_measurements_status.txt"
fi

if "$ADB" shell test -d "$OVR_METRICS_DIR"; then
    { "$ADB" shell ls -1 "$OVR_METRICS_DIR" 2>/dev/null || true; } | tr -d '\r' | sort \
        > "$OUTPUT_DIR/ovr_metrics_after.txt"
    comm -13 "$OUTPUT_DIR/ovr_metrics_before.txt" "$OUTPUT_DIR/ovr_metrics_after.txt" \
        > "$OUTPUT_DIR/ovr_metrics_new.txt"
    if [[ -s "$OUTPUT_DIR/ovr_metrics_new.txt" ]]; then
        mkdir -p "$OUTPUT_DIR/ovr_metrics"
        while IFS= read -r metrics_csv; do
            "$ADB" pull "$OVR_METRICS_DIR/$metrics_csv" "$OUTPUT_DIR/ovr_metrics/$metrics_csv"
        done < "$OUTPUT_DIR/ovr_metrics_new.txt"
        printf 'CSV novo do OVR Metrics Tool copiado.\n' > "$OUTPUT_DIR/ovr_metrics_status.txt"
    else
        printf 'O diretório do OVR Metrics Tool existe, mas nenhum CSV novo foi criado nesta execução.\n' \
            > "$OUTPUT_DIR/ovr_metrics_status.txt"
    fi
else
    printf 'Diretório de CSV do OVR Metrics Tool ausente. Instale a ferramenta e ative a gravação CSV antes da próxima repetição.\n' \
        > "$OUTPUT_DIR/ovr_metrics_status.txt"
fi

HOST_FINISHED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf '{\n  "schema_version": "1.0",\n  "run_id": "%s",\n  "condition_id": "%s",\n  "apk_path": "%s",\n  "apk_sha256": "%s",\n  "apk_size_bytes": %s,\n  "host_capture_started_at_utc": "%s",\n  "host_capture_finished_at_utc": "%s",\n  "external_capture_seconds": %s,\n  "report_wait_seconds": %s,\n  "operator_instruction_file": "operator_instruction.txt",\n  "application_measurement_index": "app_measurements_new.txt",\n  "ovr_metrics_index": "ovr_metrics_new.txt",\n  "ovr_metrics_status_file": "ovr_metrics_status.txt",\n  "notes": "Only application JSON and OVR CSV files created during this run are pulled. The app JSON contains the exact post-warmup measurement window when produced by the current APK. Source files remain on the headset."\n}\n' \
    "$RUN_ID" "$CONDITION" "$APK" "$APK_SHA256" "$APK_BYTES" "$HOST_STARTED_AT" "$HOST_FINISHED_AT" "$CAPTURE_SECONDS" "$REPORT_WAITED_SECONDS" \
    > "$OUTPUT_DIR/run_metadata.json"

printf 'Evidências preservadas em: %s\n' "$OUTPUT_DIR"
if [[ "$APPLICATION_REPORT_COMPLETE" != true ]]; then
    printf '%s\n' \
        'ERRO: o JSON da aplicação não completou a janela de medição; esta execução não deve integrar a bateria.' >&2
    exit 4
fi
