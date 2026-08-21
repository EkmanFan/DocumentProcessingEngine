#!/usr/bin/env bash
set -Eeuo pipefail
export LC_ALL=C

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFERENCE="$REPO/docs/evaluation/habermas-epub-reference-v1.json"
EPUB_FILE="${HABERMAS_EPUB_FILE:-$REPO/tests/epub_test/habermas-case-for-resurrection.epub}"
EPUBCHECK_ZIP="${EPUBCHECK_ZIP:-$REPO/scripts/tmp/tool-cache/epubcheck-5.3.0.zip}"
OUT="$REPO/scripts/tmp/epub-reference-validation"
EPUBCHECK_REPORT="$OUT/epubcheck-report.json"
CONTAINER_XML="$OUT/container.xml"
PACKAGE_DOCUMENT="$OUT/content.opf"
NAVIGATION_DOCUMENT="$OUT/nav.xhtml"
P18_CONTENT="$OUT/part0012.xhtml"
P28_CONTENT="$OUT/part0016.xhtml"
TOOL_WORK=""

cleanup() {
  if [[ -n "$TOOL_WORK" && -d "$TOOL_WORK" ]]; then
    rm -rf -- "$TOOL_WORK"
  fi
}

trap cleanup EXIT

fail() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 2
}

assert_equal() {
  local description="$1"
  local expected="$2"
  local observed="$3"

  [[ "$observed" == "$expected" ]] ||
    fail "$description: expected '$expected', observed '$observed'."
}

for command in \
  identify \
  java \
  jq \
  sha256sum \
  stat \
  unzip \
  xmllint
do
  command -v "$command" >/dev/null 2>&1 ||
    fail "$command is required."
done

[[ -f "$REFERENCE" ]] ||
  fail "EPUB reference is missing: $REFERENCE"

[[ -f "$EPUB_FILE" ]] ||
  fail "Habermas EPUB is missing: $EPUB_FILE"

[[ -f "$EPUBCHECK_ZIP" ]] ||
  fail "EPUBCheck distribution is missing: $EPUBCHECK_ZIP"

expected_source_sha="$(jq -r '.source.sha256' "$REFERENCE")"
expected_source_length="$(jq -r '.source.byteLength' "$REFERENCE")"
expected_checker_version="$(jq -r '.conformance.epubCheckVersion' "$REFERENCE")"
expected_checker_distribution_sha="$(jq -r '.conformance.epubCheckDistributionSha256' "$REFERENCE")"
expected_checker_sha="$(jq -r '.conformance.epubCheckJarSha256' "$REFERENCE")"

observed_source_sha="$(sha256sum "$EPUB_FILE" | awk '{ print $1 }')"
observed_source_length="$(stat -c '%s' "$EPUB_FILE")"
observed_checker_distribution_sha="$(sha256sum "$EPUBCHECK_ZIP" | awk '{ print $1 }')"

assert_equal "EPUB SHA-256" "$expected_source_sha" "$observed_source_sha"
assert_equal "EPUB byte length" "$expected_source_length" "$observed_source_length"
assert_equal \
  "EPUBCheck distribution SHA-256" \
  "$expected_checker_distribution_sha" \
  "$observed_checker_distribution_sha"

mkdir -p "$OUT"
TOOL_WORK="$(mktemp -d "$OUT/epubcheck.XXXXXX")"
unzip -q "$EPUBCHECK_ZIP" -d "$TOOL_WORK"
EPUBCHECK_JAR="$TOOL_WORK/epubcheck-$expected_checker_version/epubcheck.jar"

[[ -f "$EPUBCHECK_JAR" ]] ||
  fail "EPUBCheck JAR is missing from the pinned distribution."

observed_checker_sha="$(sha256sum "$EPUBCHECK_JAR" | awk '{ print $1 }')"
observed_checker_version="$(
  java -jar "$EPUBCHECK_JAR" --version |
    awk '{ sub(/^v/, "", $2); print $2 }'
)"

assert_equal "EPUBCheck version" "$expected_checker_version" "$observed_checker_version"
assert_equal "EPUBCheck JAR SHA-256" "$expected_checker_sha" "$observed_checker_sha"

rm -f \
  "$EPUBCHECK_REPORT" \
  "$CONTAINER_XML" \
  "$PACKAGE_DOCUMENT" \
  "$NAVIGATION_DOCUMENT" \
  "$P18_CONTENT" \
  "$P28_CONTENT"

printf 'DPEngine EPUB-0 reference validation\n'
printf 'Reference: %s\n' "$REFERENCE"
printf 'EPUB: %s\n' "$EPUB_FILE"
printf 'EPUBCheck distribution: %s (%s)\n\n' \
  "$EPUBCHECK_ZIP" \
  "$observed_checker_version"

printf '[1/3] Checking EPUB 3.3 conformance...\n'
java -jar "$EPUBCHECK_JAR" \
  "$EPUB_FILE" \
  --json "$EPUBCHECK_REPORT"

jq -e \
  --arg version "$expected_checker_version" \
  --arg title "$(jq -r '.publication.title' "$REFERENCE")" \
  --arg identifier "$(jq -r '.publication.identifier' "$REFERENCE")" \
  --arg language "$(jq -r '.publication.language' "$REFERENCE")" \
  --arg epubVersion "$(jq -r '.publication.epubVersion' "$REFERENCE")" \
  --arg renditionLayout "$(jq -r '.publication.renditionLayout' "$REFERENCE")" \
  --argjson fatalCount "$(jq -r '.conformance.fatalCount' "$REFERENCE")" \
  --argjson errorCount "$(jq -r '.conformance.errorCount' "$REFERENCE")" \
  --argjson warningCount "$(jq -r '.conformance.warningCount' "$REFERENCE")" \
  --argjson usageCount "$(jq -r '.conformance.usageCount' "$REFERENCE")" \
  --argjson spineItemCount "$(jq -r '.publication.spineItemCount' "$REFERENCE")" \
  '
    .checker.checkerVersion == $version and
    .checker.nFatal == $fatalCount and
    .checker.nError == $errorCount and
    .checker.nWarning == $warningCount and
    .checker.nUsage == $usageCount and
    (.messages | length) == ($fatalCount + $errorCount + $warningCount + $usageCount) and
    .publication.title == $title and
    .publication.identifier == $identifier and
    .publication.language == $language and
    .publication.ePubVersion == $epubVersion and
    .publication.renditionLayout == $renditionLayout and
    .publication.nSpines == $spineItemCount
  ' \
  "$EPUBCHECK_REPORT" >/dev/null ||
  fail "EPUBCheck report differs from the frozen publication reference."

printf '\n[2/3] Checking package and reading order...\n'
unzip -p "$EPUB_FILE" META-INF/container.xml > "$CONTAINER_XML"

observed_package_document="$(
  xmllint --xpath \
    "string(/*[local-name()='container']/*[local-name()='rootfiles']/*[local-name()='rootfile'][1]/@full-path)" \
    "$CONTAINER_XML"
)"
expected_package_document="$(jq -r '.publication.packageDocument' "$REFERENCE")"

assert_equal \
  "Package-document path" \
  "$expected_package_document" \
  "$observed_package_document"

unzip -p "$EPUB_FILE" "$observed_package_document" > "$PACKAGE_DOCUMENT"

observed_navigation_href="$(
  xmllint --xpath \
    "string(/*[local-name()='package']/*[local-name()='manifest']/*[local-name()='item'][contains(concat(' ', normalize-space(@properties), ' '), ' nav ')]/@href)" \
    "$PACKAGE_DOCUMENT"
)"
observed_package_directory="${observed_package_document%/*}"
observed_navigation_document="$observed_package_directory/$observed_navigation_href"

assert_equal \
  "Navigation-document path" \
  "$(jq -r '.publication.navigationDocument' "$REFERENCE")" \
  "$observed_navigation_document"

unzip -p "$EPUB_FILE" "$observed_navigation_document" > \
  "$NAVIGATION_DOCUMENT"

observed_spine_count="$(
  xmllint --xpath \
    "count(/*[local-name()='package']/*[local-name()='spine']/*[local-name()='itemref'])" \
    "$PACKAGE_DOCUMENT"
)"

assert_equal \
  "Spine item count" \
  "$(jq -r '.publication.spineItemCount' "$REFERENCE")" \
  "$observed_spine_count"

for control_index in 0 1
do
  spine_index="$(jq -r ".controls[$control_index].spineIndex" "$REFERENCE")"
  expected_href="$(
    jq -r ".controls[$control_index].contentDocument | sub(\"^OEBPS/\"; \"\")" \
      "$REFERENCE"
  )"
  xpath_index="$((spine_index + 1))"
  observed_idref="$(
    xmllint --xpath \
      "string((/*[local-name()='package']/*[local-name()='spine']/*[local-name()='itemref'])[$xpath_index]/@idref)" \
      "$PACKAGE_DOCUMENT"
  )"
  observed_href="$(
    xmllint --xpath \
      "string(/*[local-name()='package']/*[local-name()='manifest']/*[local-name()='item'][@id='$observed_idref']/@href)" \
      "$PACKAGE_DOCUMENT"
  )"

  assert_equal \
    "Spine item $spine_index" \
    "$expected_href" \
    "$observed_href"

  print_page_marker="$(jq -r ".controls[$control_index].printPageMarker" "$REFERENCE")"
  expected_page_href="$expected_href#page_$print_page_marker"
  observed_page_href="$(
    xmllint --xpath \
      "string((//*[local-name()='nav'][@*[local-name()='type']='page-list']//*[local-name()='a'][normalize-space(.)='$print_page_marker'])[1]/@href)" \
      "$NAVIGATION_DOCUMENT"
  )"

  assert_equal \
    "Print-page marker $print_page_marker" \
    "$expected_page_href" \
    "$observed_page_href"
done

printf '\n[3/3] Checking Habermas p18/p28 reference observations...\n'
unzip -p "$EPUB_FILE" \
  "$(jq -r '.controls[0].contentDocument' "$REFERENCE")" > \
  "$P18_CONTENT"
unzip -p "$EPUB_FILE" \
  "$(jq -r '.controls[1].contentDocument' "$REFERENCE")" > \
  "$P28_CONTENT"

for control_index in 0 1
do
  image_path="$(jq -r ".controls[$control_index].image.path" "$REFERENCE")"
  expected_image_sha="$(jq -r ".controls[$control_index].image.sha256" "$REFERENCE")"
  expected_image_length="$(jq -r ".controls[$control_index].image.byteLength" "$REFERENCE")"
  expected_dimensions="$(
    jq -r \
      ".controls[$control_index].image | \"\\(.pixelWidth)x\\(.pixelHeight)\"" \
      "$REFERENCE"
  )"
  observed_image_sha="$(unzip -p "$EPUB_FILE" "$image_path" | sha256sum | awk '{ print $1 }')"
  observed_image_length="$(unzip -p "$EPUB_FILE" "$image_path" | wc -c | tr -d ' ')"
  observed_dimensions="$(
    unzip -p "$EPUB_FILE" "$image_path" |
      identify -format '%wx%h' -
  )"
  image_href="${image_path#OEBPS/}"
  observed_image_media_type="$(
    xmllint --xpath \
      "string(/*[local-name()='package']/*[local-name()='manifest']/*[local-name()='item'][@href='$image_href']/@media-type)" \
      "$PACKAGE_DOCUMENT"
  )"
  content_file="$P18_CONTENT"
  if [[ "$control_index" == 1 ]]; then
    content_file="$P28_CONTENT"
  fi
  observed_image_reference_count="$(
    xmllint --xpath \
      "count(//*[local-name()='img'][@src='$(basename "$image_path")'])" \
      "$content_file"
  )"

  assert_equal "Image SHA-256" "$expected_image_sha" "$observed_image_sha"
  assert_equal "Image byte length" "$expected_image_length" "$observed_image_length"
  assert_equal "Image dimensions" "$expected_dimensions" "$observed_dimensions"
  assert_equal \
    "Image media type" \
    "$(jq -r ".controls[$control_index].image.mediaType" "$REFERENCE")" \
    "$observed_image_media_type"
  assert_equal "Image reference count" "1" "$observed_image_reference_count"
done

p28_image_name="$(basename "$(jq -r '.controls[1].image.path' "$REFERENCE")")"
post_image_paragraph_xpath="(//*[local-name()='img'][@src='$p28_image_name']/parent::*[1]/following-sibling::*[1])[1]"
following_heading_xpath="(//*[local-name()='img'][@src='$p28_image_name']/parent::*[1]/following-sibling::*[2])[1]"

observed_post_image_element="$(
  xmllint --xpath \
    "local-name($post_image_paragraph_xpath)" \
    "$P28_CONTENT"
)"
observed_post_image_text="$(
  xmllint --xpath \
    "normalize-space(string($post_image_paragraph_xpath))" \
    "$P28_CONTENT"
)"
observed_post_image_sha="$(printf '%s' "$observed_post_image_text" | sha256sum | awk '{ print $1 }')"
observed_heading_element="$(
  xmllint --xpath \
    "local-name($following_heading_xpath)" \
    "$P28_CONTENT"
)"
observed_heading_text="$(
  xmllint --xpath \
    "normalize-space(string($following_heading_xpath))" \
    "$P28_CONTENT"
)"
observed_heading_sha="$(printf '%s' "$observed_heading_text" | sha256sum | awk '{ print $1 }')"
observed_figcaption_count="$(
  xmllint --xpath \
    "count(//*[local-name()='figcaption'])" \
    "$P28_CONTENT"
)"

assert_equal \
  "Post-image paragraph element" \
  "$(jq -r '.controls[1].postImageParagraph.elementName' "$REFERENCE")" \
  "$observed_post_image_element"
assert_equal \
  "Post-image paragraph SHA-256" \
  "$(jq -r '.controls[1].postImageParagraph.normalizedTextSha256' "$REFERENCE")" \
  "$observed_post_image_sha"
assert_equal \
  "Following heading element" \
  "$(jq -r '.controls[1].followingHeading.elementName' "$REFERENCE")" \
  "$observed_heading_element"
assert_equal \
  "Following heading text" \
  "$(jq -r '.controls[1].followingHeading.normalizedText' "$REFERENCE")" \
  "$observed_heading_text"
assert_equal \
  "Following heading SHA-256" \
  "$(jq -r '.controls[1].followingHeading.normalizedTextSha256' "$REFERENCE")" \
  "$observed_heading_sha"
assert_equal \
  "Figure-caption element count" \
  "$(jq -r '.controls[1].figcaptionCount' "$REFERENCE")" \
  "$observed_figcaption_count"

printf '\nEPUB-0 REFERENCE VALIDATION: PASS\n'
printf 'EPUBCheck report: %s\n' "$EPUBCHECK_REPORT"
