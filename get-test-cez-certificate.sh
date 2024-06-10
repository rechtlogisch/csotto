#!/bin/bash

TARGET_DIRECTORY="CEZ/"
CERTIFICATE_ARCHIVE_FILENAME="Test_Zertifikate.zip"

curl -sS -o "$TARGET_DIRECTORY$CERTIFICATE_ARCHIVE_FILENAME" https://download.elster.de/download/schnittstellen/"$CERTIFICATE_ARCHIVE_FILENAME" && \
unzip -j "$TARGET_DIRECTORY$CERTIFICATE_ARCHIVE_FILENAME" -d "$TARGET_DIRECTORY" eric-zertifikate-bescheidabholung/PSE/eric_private.p12 eric-zertifikate-bescheidabholung/PSE/eric_public.cer && \
rm "$TARGET_DIRECTORY$CERTIFICATE_ARCHIVE_FILENAME"
