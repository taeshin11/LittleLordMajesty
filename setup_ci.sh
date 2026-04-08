#!/bin/bash
# ============================================================
# LittleLordMajesty CI Setup Script
# Run this once to enable GitHub Actions Unity builds
# ============================================================
# Usage: bash setup_ci.sh
# Requires: gh CLI (already installed), Unity license on this machine

set -e

REPO="taeshin11/LittleLordMajesty"

echo "=== LittleLordMajesty CI Setup ==="
echo "This will set GitHub secrets and enable Unity builds."
echo ""

# Get Unity credentials
read -p "Unity account email: " UNITY_EMAIL
read -s -p "Unity account password: " UNITY_PASSWORD
echo ""

if [ -z "$UNITY_EMAIL" ] || [ -z "$UNITY_PASSWORD" ]; then
  echo "ERROR: Email and password are required."
  exit 1
fi

echo ""
echo "Setting GitHub secrets..."

# Set secrets
gh secret set UNITY_EMAIL --body "$UNITY_EMAIL" --repo "$REPO"
echo "✅ UNITY_EMAIL set"

gh secret set UNITY_PASSWORD --body "$UNITY_PASSWORD" --repo "$REPO"
echo "✅ UNITY_PASSWORD set"

# Try to use local license XML as UNITY_LICENSE
LICENSE_FILE="C:/Users/$USERNAME/AppData/Local/Unity/licenses/UnityEntitlementLicense.xml"
if [ -f "$LICENSE_FILE" ]; then
  gh secret set UNITY_LICENSE --body "$(cat "$LICENSE_FILE")" --repo "$REPO"
  echo "✅ UNITY_LICENSE set (from local Unity license)"
fi

# Enable builds
gh variable set UNITY_BUILD_ENABLED --body "true" --repo "$REPO"
echo "✅ UNITY_BUILD_ENABLED set to true"

echo ""
echo "=== Done! ==="
echo "Next push to master will trigger WebGL + Android builds."
echo "Monitor at: https://github.com/$REPO/actions"
