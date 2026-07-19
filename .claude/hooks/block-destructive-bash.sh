#!/usr/bin/env bash
# Protocole JSON structuré (hookSpecificOutput.permissionDecision) : le champ JSON
# détermine le blocage, indépendamment du code de sortie (exit 0 partout ci-dessous,
# y compris sur deny). Ce n'est PAS le protocole legacy basé sur exit 2 — ne pas "corriger".
#
# Limite acceptée (vérifiée par exécution) : regex sur la chaîne brute, pas sémantique.
# Faux positifs possibles si le texte destructeur apparaît dans une chaîne (ex: message de
# commit citant "git reset --hard", echo décrivant "rm -rf"). Contournable via encodage/alias.
# Compromis assumé : simplicité > exhaustivité, coût du faux positif = reformuler la commande.
#
# Fail-open SIGNALÉ : si jq est absent ou si le parsing échoue (JSON inattendu,
# code de sortie non-zéro, champ manquant), le hook ne peut pas parser la commande
# et n'offre AUCUNE PROTECTION. Dans les deux cas, un avertissement est émis sur
# stderr (garde-fou DÉSACTIVÉ) puis le hook autorise (exit 0). Avertissement
# runtime best-effort — le filet fiable reste de vérifier jq à l'installation.
# Prérequis : winget install jqlang.jq (Windows) ou brew install jq / apt install jq.
if ! command -v jq >/dev/null 2>&1; then
  echo "[mA.xI.me] jq introuvable — garde-fou anti-commandes-destructrices DÉSACTIVÉ." >&2
  exit 0
fi
input="$(cat)"
cmd="$(printf '%s' "$input" | jq -r '.tool_input.command // empty' 2>&1)"
jq_exit=$?
if [ $jq_exit -ne 0 ]; then
  echo "[mA.xI.me] jq a échoué (exit $jq_exit) — garde-fou DÉSACTIVÉ pour cette commande. Détail: $cmd" >&2
  exit 0
fi
[ -z "$cmd" ] && exit 0

# Irréversible, aucun usage légitime automatisé dans ce repo → DENY dur
hard_deny='rm[[:space:]]+(-[A-Za-z]+[[:space:]]+)*-[A-Za-z]*(rf|fr)[A-Za-z]*\b'
hard_deny+='|rm([[:space:]].*)?[[:space:]]-r\b.*[[:space:]]-f\b'
hard_deny+='|rm([[:space:]].*)?[[:space:]]-f\b.*[[:space:]]-r\b'
hard_deny+='|git[[:space:]]+reset[[:space:]]+--hard'
hard_deny+='|git[[:space:]]+clean[[:space:]]+[^|;&]*-[A-Za-z]*f'
hard_deny+='|git[[:space:]]+checkout[[:space:]]+--([[:space:]]|$)'
hard_deny+='|git[[:space:]]+checkout[[:space:]]+\.([[:space:]]|$)'
hard_deny+='|git[[:space:]]+branch[[:space:]]+(-D|--delete[[:space:]]+--force)'
hard_deny+='|git[[:space:]]+add[[:space:]]+(-A|--all)([[:space:]]|$)'
hard_deny+='|git[[:space:]]+(checkout|switch)[[:space:]]+(main|master)([[:space:]]|$)'
hard_deny+='|(^|[;&|])[[:space:]]*(echo|printf|cat)[^;&|]*(>|>>)'
hard_deny+='|(^|[;&|])[[:space:]]*(Set-Content|Out-File|New-Item|Remove-Item|Move-Item|Copy-Item)\b'

# Risqué mais parfois légitime en interactif → ASK (force un prompt humain)
soft_ask='git[[:space:]]+push[[:space:]]+[^|;&]*(--force\b|-f\b|--force-with-lease)'
soft_ask+='|git[[:space:]]+push[[:space:]]+[^|;&]*--delete'

if echo "$cmd" | grep -qE "$hard_deny"; then
  jq -n --arg reason "Commande destructrice/irréversible bloquée par garde-fou repo: $cmd" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$reason}}'
  exit 0
fi

if echo "$cmd" | grep -qE "$soft_ask"; then
  jq -n --arg reason "Commande à risque (push force/delete) — confirmation requise: $cmd" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"ask",permissionDecisionReason:$reason}}'
  exit 0
fi

exit 0