#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <version> <publish-dir> <output-dir>" >&2
  exit 1
fi

version="${1#v}"
publish_dir="$2"
output_dir="$3"

if [[ ! -x "$publish_dir/RecipeStudio.Desktop" ]]; then
  echo "Linux publish executable not found: $publish_dir/RecipeStudio.Desktop" >&2
  exit 1
fi

package_name="recipestudio"
package_root="$(mktemp -d)"
deb_root="$package_root/${package_name}_${version}_amd64"
app_dir="$deb_root/opt/RecipeStudio"
doc_dir="$deb_root/usr/share/doc/$package_name"

cleanup() {
  rm -rf "$package_root"
}
trap cleanup EXIT

mkdir -p \
  "$deb_root/DEBIAN" \
  "$app_dir" \
  "$deb_root/usr/bin" \
  "$deb_root/usr/share/applications" \
  "$doc_dir"

install -m 755 "$publish_dir/RecipeStudio.Desktop" "$app_dir/RecipeStudio.Desktop"
install -m 644 RELEASE_README.md "$doc_dir/README.md"

cat > "$deb_root/usr/bin/recipestudio" <<'EOF'
#!/usr/bin/env sh
exec /opt/RecipeStudio/RecipeStudio.Desktop "$@"
EOF
chmod 755 "$deb_root/usr/bin/recipestudio"

cat > "$deb_root/usr/share/applications/recipestudio.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=RecipeStudio
Exec=/usr/bin/recipestudio
Terminal=false
Categories=Utility;
StartupWMClass=RecipeStudio.Desktop
EOF
chmod 644 "$deb_root/usr/share/applications/recipestudio.desktop"

cat > "$deb_root/DEBIAN/control" <<EOF
Package: $package_name
Version: $version
Section: utils
Priority: optional
Architecture: amd64
Maintainer: RecipeStudio
Depends: libfontconfig1, libfreetype6, libgl1, libx11-6, libxrandr2, libxi6, xdg-utils
Description: RecipeStudio desktop application
 RecipeStudio is an Avalonia-based desktop tool for recipe preparation,
 calculation, visualization, and Excel import/export.
EOF
chmod 644 "$deb_root/DEBIAN/control"

mkdir -p "$output_dir"
dpkg-deb --build --root-owner-group "$deb_root" "$output_dir/recipestudio_${version}_amd64.deb"
