#!/usr/bin/env bash
# develop → main の PR を開く。すでに開いている、または差分がない場合は何もしない。
# main は protect-main ルールセットで PR 必須のため、GITHUB_TOKEN では直接 push できない。
# 必要な環境変数: GH_TOKEN, GH_REPO
set -euo pipefail

existing=$(gh pr list --base main --head develop --state open --json url --jq '.[0].url // empty')
if [ -n "$existing" ]; then
  echo "Pull request already open: $existing" | tee -a "${GITHUB_STEP_SUMMARY:-/dev/null}"
  exit 0
fi

ahead=$(gh api "repos/$GH_REPO/compare/main...develop" --jq '.ahead_by')
if [ "$ahead" = "0" ]; then
  echo "develop has no commits to promote." | tee -a "${GITHUB_STEP_SUMMARY:-/dev/null}"
  exit 0
fi

body="$(mktemp)"
{
  echo "develop の変更を main に取り込みます。"
  echo
  echo "## Commits"
  gh api "repos/$GH_REPO/compare/main...develop" \
    --jq '.commits[] | select(.parents | length == 1) | "- \(.commit.message | split("\n")[0]) (\(.sha[0:7]))"'
} >"$body"

url=$(gh pr create --base main --head develop --title "Promote develop to main" --body-file "$body")
rm -f "$body"
echo "Opened: $url" | tee -a "${GITHUB_STEP_SUMMARY:-/dev/null}"
