const frame = document.getElementById("unity-frame");
const shell = document.getElementById("unity-shell");

if (frame && shell) {
  frame.addEventListener("load", () => {
    shell.classList.add("is-ready");
  });
}
