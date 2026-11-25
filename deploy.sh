#!/bin/bash
# 1. Package the folder into an UNCOMPRESSED tar archive (-cf instead of -czf)
# This preserves your .br files exactly as they are.
tar -cf build.tar -C ./YourUnityBuildFolder .

# 2. Upload the tarball to your VM
gcloud compute scp build.tar instance-20250801-113609:/tmp/build.tar \
    --zone=us-central1-c \
    --project=perfect-altar-409523

# 3. Extract the tarball directly into your web root and clean up
gcloud compute ssh instance-20250801-113609 \
    --zone=us-central1-c \
    --project=perfect-altar-409523 \
    --command="sudo tar -xf /tmp/build.tar -C /var/www/html && rm /tmp/build.tar"

echo "Deployment of Brotli build complete!"
