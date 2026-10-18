#!/usr/bin/env python3
"""
Uploads a build (or any file) to Cloudflare R2 and prints a presigned download link.

R2 must be enabled in the Cloudflare dashboard first:
    https://dash.cloudflare.com -> R2 -> Enable R2 (free tier requires a card)

Usage:
    python3 Tools/upload_r2.py build/StandaloneWindows64 iron-district-builds
"""
import sys, os, boto3
from botocore.config import Config

ENDPOINT = os.environ.get(
    "R2_ENDPOINT",
    "https://3a6eeef666b57507d834a828fc6cd883.r2.cloudflarestorage.com",
)
KEY = os.environ.get("R2_ACCESS_KEY_ID", "ef18a87a221c5498443acfc89a532c97")
SECRET = os.environ.get(
    "R2_SECRET_ACCESS_KEY",
    "8c634b81b73c47bb3a3d8bb7ab7a4f723bb35739e144e8ae04a2a61f2287f2a8",
)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    local = sys.argv[1]
    bucket = sys.argv[2] if len(sys.argv) > 2 else "iron-district-builds"

    s3 = boto3.client(
        "s3",
        endpoint_url=ENDPOINT,
        aws_access_key_id=KEY,
        aws_secret_access_key=SECRET,
        region_name="auto",
        config=Config(signature_version="s3v4"),
    )

    try:
        existing = [b["Name"] for b in s3.list_buckets().get("Buckets", [])]
        if bucket not in existing:
            s3.create_bucket(Bucket=bucket)
            print("bucket created:", bucket)
    except Exception as e:
        print("R2 is not reachable:", type(e).__name__, e)
        print("Likely R2 is not enabled yet: Cloudflare dashboard -> R2 -> Enable R2")
        return 2

    uploaded = []
    if os.path.isdir(local):
        for root, _, files in os.walk(local):
            for f in files:
                path = os.path.join(root, f)
                key = os.path.relpath(path, local).replace(os.sep, "/")
                s3.upload_file(path, bucket, key)
                uploaded.append(key)
    else:
        key = os.path.basename(local)
        s3.upload_file(local, bucket, key)
        uploaded.append(key)

    print("uploaded:", len(uploaded), "files")
    url = s3.generate_presigned_url(
        "get_object",
        Params={"Bucket": bucket, "Key": uploaded[0]},
        ExpiresIn=7 * 24 * 3600,
    )
    print("sample presigned URL (7 days):", url)
    return 0


if __name__ == "__main__":
    sys.exit(main())
