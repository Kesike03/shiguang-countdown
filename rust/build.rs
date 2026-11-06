use std::env;
use std::path::PathBuf;
use std::process::Command;

fn main() {
    println!("cargo:rerun-if-changed=app.rc");
    println!("cargo:rerun-if-changed=app.ico");
    let out = PathBuf::from(env::var("OUT_DIR").unwrap());
    let obj = out.join("app_icon.o");
    let status = Command::new("windres")
        .arg("app.rc")
        .arg("-O")
        .arg("coff")
        .arg("-o")
        .arg(&obj)
        .status()
        .expect("windres not available");
    assert!(status.success(), "windres failed");
    println!("cargo:rustc-link-arg={}", obj.display());
}
