#!/bin/bash
set -e

echo "🎬 ClubBaist Demo Recording Script"
echo "===================================="

# Start AppHost in background
cd /Users/allyn/Repos/ClubBaist-test
echo "Starting Aspire AppHost..."
dotnet run --project ClubBaist.AppHost/ClubBaist.AppHost.csproj > /tmp/apphost.log 2>&1 &
APPHOST_PID=$!
echo "AppHost PID: $APPHOST_PID"

# Wait for app to be ready
echo "Waiting for app to be ready..."
for i in {1..60}; do
  if curl -sk -o /dev/null -w '%{http_code}' https://localhost:7021/Account/Login 2>/dev/null | grep -q '200'; then
    echo "✅ App is ready"
    break
  fi
  echo "  Attempt $i/60..."
  sleep 2
done

# Run the recording
echo ""
echo "Starting Playwright recording (4-8 minutes)..."
echo "You will see Chrome open and automatically run through the demo..."
echo ""

cd demo-script
export BASE_URL=https://localhost:7021
export DEMO_SLOWMO=100
node record-detailed-demo.mjs

RECORD_EXIT=$?

echo ""
echo "===================================="
if [ $RECORD_EXIT -eq 0 ]; then
  echo "✅ Recording completed successfully!"
  echo ""
  ls -lh ../videos/
else
  echo "❌ Recording failed with exit code $RECORD_EXIT"
fi

# Cleanup
echo "Cleaning up..."
kill $APPHOST_PID 2>/dev/null || true
wait $APPHOST_PID 2>/dev/null || true

exit $RECORD_EXIT
