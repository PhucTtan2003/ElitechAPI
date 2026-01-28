import React from "react";
import Dashboard from "./pages/Dashboard";
import { getMe } from "./services/api";
import type { MeInfo } from "./type";
import UserDevicesPanel from "./pages/UserDevicesPanel";

export default function App({ who }: { who: "Admin" | "User" }) {
  const [me, setMe] = React.useState<MeInfo | null>(null);

  React.useEffect(() => {
    getMe().then(setMe).catch(() => setMe(null));
  }, []);

  // ✅ User view
  if (who === "User") {
    return (
      <div>
        <h5 className="mb-2">User Dashboard</h5>
        <div className="mb-1">
          Xin chào: <strong>{me?.name ?? "(ẩn danh)"}</strong>
        </div>
        <div className="text-muted small mb-3">Role: {me?.role ?? "-"}</div>

        <UserDevicesPanel />
      </div>
    );
  }

  // ✅ Admin view
  return (
    <div>
      <h5 className="mb-2">React {who} Dashboard</h5>
      <div className="mb-1">
        Xin chào: <strong>{me?.name ?? "(ẩn danh)"}</strong>
      </div>
      <div className="text-muted small mb-3">Role: {me?.role ?? "-"}</div>

      <Dashboard who={who} />
    </div>
  );
}
