import React from "react";
import ReactDOM from "react-dom/client";
import AdminRoot from "./AdminRoot";

console.log("🔥 entry-admin loaded");

const el =
  document.getElementById("root") ||
  document.getElementById("react-root-admin");

if (!el) {
  console.error("❌ No mount element found (#root or #react-root-admin)");
} else {
  console.log("✅ Mounting to:", el.id);
  ReactDOM.createRoot(el).render(
    <React.StrictMode>
      <AdminRoot />
    </React.StrictMode>
  );
}
