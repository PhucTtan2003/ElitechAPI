import { useEffect, useState } from "react";
import { fetchMyDevices } from "../services/userApi";

type DeviceItem = { deviceGuid: string; deviceName?: string };
type ApiResp<T> = { code: number; message?: string; data?: T };

function errText(e: unknown) {
  if (e instanceof Error) return e.message;
  if (typeof e === "string") return e;
  try { return JSON.stringify(e); } catch { return "Unknown error"; }
}

export default function UserDevicesPanel() {
  const [devices, setDevices] = useState<DeviceItem[]>([]);
  const [selected, setSelected] = useState("");
  const [msg, setMsg] = useState("");

  useEffect(() => {
    fetchMyDevices()
      .then((r: ApiResp<DeviceItem[]>) => {
        const list = r.data ?? [];
        setDevices(list);
        if (list.length) setSelected(list[0].deviceGuid);
      })
      .catch((e) => setMsg(`❌ ${errText(e)}`));
  }, []);

  function goChart() {
    if (!selected) return;
    // đi thẳng tới trang Razor chart bạn đã làm
    window.location.href = `/history?deviceGuid=${encodeURIComponent(selected)}`;
  }

  return (
    <div style={{ maxWidth: 900 }}>
      <div style={{ display: "flex", gap: 12, flexWrap: "wrap", alignItems: "end" }}>
        <div style={{ minWidth: 320, flex: 1 }}>
          <div style={{ fontWeight: 800, marginBottom: 6 }}>Thiết bị được gán</div>
          <select
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
            style={{ width: "100%", padding: 10, borderRadius: 12, border: "1px solid #e7ebf3" }}
          >
            {devices.length === 0 && <option value="">Chưa có thiết bị</option>}
            {devices.map(d => (
              <option key={d.deviceGuid} value={d.deviceGuid}>
                {d.deviceName ? `${d.deviceName} — ${d.deviceGuid}` : d.deviceGuid}
              </option>
            ))}
          </select>
        </div>

        <button
        type="button"
        className="btn primary"
        onClick={goChart}
        disabled={!selected}
        >
        📈 Xem biểu đồ
        </button>
      </div>

      {msg && <div style={{ marginTop: 10, fontWeight: 900 }}>{msg}</div>}

      <div className="text-muted" style={{ marginTop: 10 }}>
        * User chỉ xem được thiết bị đã được Admin gán.
      </div>
    </div>
  );
}
