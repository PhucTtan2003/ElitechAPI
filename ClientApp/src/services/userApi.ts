type ApiResp<T> = { code: number; message?: string; data?: T };

export async function fetchMyDevices() {
  const r = await fetch("/api/elitech/my-devices", { credentials: "include" });
  if (!r.ok) throw new Error(`my-devices failed: ${r.status}`);
  return (await r.json()) as ApiResp<{ deviceGuid: string; deviceName?: string }[]>;
}