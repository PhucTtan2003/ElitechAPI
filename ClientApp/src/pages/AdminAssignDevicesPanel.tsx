import React, { useEffect, useMemo, useState } from "react";
import {
  assignDevice,
  fetchAssignmentsByUser,
  fetchUsers,
  unassignDevice,
} from "../services/adminApi";
import "../../styles/AdminAssignDevicePanel.css"

type UserItem = { userId: string; username: string };
type AssignmentItem = { deviceGuid: string; deviceName?: string };

type ApiResp<T> = { code: number; message?: string; data?: T };

function errText(e: unknown) {
  if (e instanceof Error) return e.message;
  if (typeof e === "string") return e;
  try {
    return JSON.stringify(e);
  } catch {
    return "Unknown error";
  }
}

type MsgKind = "ok" | "warn" | "err" | "";

export default function AdminAssignDevicesPanel() {
  const [open, setOpen] = useState(true);

  const [users, setUsers] = useState<UserItem[]>([]);
  const [userId, setUserId] = useState("");
  const [deviceGuid, setDeviceGuid] = useState("");
  const [deviceName, setDeviceName] = useState("");
  const [rows, setRows] = useState<AssignmentItem[]>([]);
  const [loadingUsers, setLoadingUsers] = useState(false);
  const [loadingRows, setLoadingRows] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [msg, setMsg] = useState<string>("");
  const [msgKind, setMsgKind] = useState<MsgKind>("");

  const [userQuery, setUserQuery] = useState("");

  // Expose toggle cho Razor bằng custom event (KHÔNG any)
  useEffect(() => {
    const onOpen: EventListener = () => setOpen(true);
    const onClose: EventListener = () => setOpen(false);
    const onToggle: EventListener = () => setOpen((v) => !v);

    window.addEventListener("elitech:assign:open", onOpen);
    window.addEventListener("elitech:assign:close", onClose);
    window.addEventListener("elitech:assign:toggle", onToggle);

    return () => {
      window.removeEventListener("elitech:assign:open", onOpen);
      window.removeEventListener("elitech:assign:close", onClose);
      window.removeEventListener("elitech:assign:toggle", onToggle);
    };
  }, []);

  function setBanner(kind: MsgKind, text: string) {
    setMsgKind(kind);
    setMsg(text);
  }

  useEffect(() => {
    (async () => {
      try {
        setLoadingUsers(true);
        const r: ApiResp<UserItem[]> = await fetchUsers();
        setUsers(r.data ?? []);
      } catch (e: unknown) {
        setBanner("err", `❌ ${errText(e)}`);
      } finally {
        setLoadingUsers(false);
      }
    })();
  }, []);

  async function reloadAssignments(uid: string) {
    if (!uid) {
      setRows([]);
      return;
    }
    try {
      setLoadingRows(true);
      const r: ApiResp<AssignmentItem[]> = await fetchAssignmentsByUser(uid);
      if (r.code !== 0) {
        setRows([]);
        setBanner("err", r.message ? `❌ ${r.message}` : "❌ Lỗi tải danh sách thiết bị");
        return;
      }
      setRows(r.data ?? []);
    } finally {
      setLoadingRows(false);
    }
  }

  useEffect(() => {
    reloadAssignments(userId).catch((e: unknown) => setBanner("err", `❌ ${errText(e)}`));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const filteredUsers = useMemo(() => {
    const q = userQuery.trim().toLowerCase();
    if (!q) return users;
    return users.filter((u) => u.username.toLowerCase().includes(q) || u.userId.toLowerCase().includes(q));
  }, [users, userQuery]);

  const canSubmit = useMemo(() => !!userId && !!deviceGuid.trim(), [userId, deviceGuid]);

  const selectedUser = useMemo(() => users.find((u) => u.userId === userId), [users, userId]);

  const rowsSorted = useMemo(() => {
    return [...rows].sort((a, b) => (a.deviceName ?? "").localeCompare(b.deviceName ?? "") || a.deviceGuid.localeCompare(b.deviceGuid));
  }, [rows]);

  async function onAssign() {
    try {
      if (!canSubmit || submitting) return;
      setBanner("", "");
      setSubmitting(true);

      await assignDevice(userId, deviceGuid.trim(), deviceName.trim() || undefined);

      setBanner("ok", "✅ Đã gán thiết bị");
      setDeviceGuid("");
      setDeviceName("");
      await reloadAssignments(userId);
    } catch (e: unknown) {
      setBanner("err", `❌ ${errText(e)}`);
    } finally {
      setSubmitting(false);
    }
  }

  async function onUnassign(guid: string) {
    try {
      if (!userId || submitting) return;
      const ok = window.confirm(`Gỡ thiết bị ${guid} khỏi user này?`);
      if (!ok) return;

      setBanner("", "");
      setSubmitting(true);

      await unassignDevice(userId, guid);

      setBanner("warn", "🗑️ Đã gỡ thiết bị");
      await reloadAssignments(userId);
    } catch (e: unknown) {
      setBanner("err", `❌ ${errText(e)}`);
    } finally {
      setSubmitting(false);
    }
  }

  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      setBanner("ok", "📋 Đã copy");
      window.setTimeout(() => setBanner("", ""), 900);
    } catch {
      // ignore
    }
  }

  if (!open) {
    return (
      <div className="asgn-muted">
        <small>Panel gán/gỡ đang ẩn. Bấm “Gán thiết bị” để mở.</small>
      </div>
    );
  }

  return (
    <div className="asgn-card">
      <div className="asgn-head">
        <div className="asgn-title">
          <div className="asgn-icon">🔧</div>
          <div>
            <div className="asgn-h1">Gán / Gỡ thiết bị cho User</div>
            <div className="asgn-sub">
              {selectedUser ? (
                <>
                  Đang chọn: <b>{selectedUser.username}</b> <span className="asgn-dot">•</span>{" "}
                  <span className="asgn-mono">{selectedUser.userId}</span>
                </>
              ) : (
                "Chọn user để xem danh sách thiết bị đã gán"
              )}
            </div>
          </div>
        </div>

        <div className="asgn-headActions">
          <button className="asgn-btn ghost" onClick={() => reloadAssignments(userId)} disabled={!userId || loadingRows}>
            {loadingRows ? "Đang tải..." : "↻ Tải lại"}
          </button>
          <button className="asgn-btn" onClick={() => setOpen(false)}>
            Đóng
          </button>
        </div>
      </div>

      {msg && (
        <div className={`asgn-banner ${msgKind ? `is-${msgKind}` : ""}`}>
          <span>{msg}</span>
        </div>
      )}

      <div className="asgn-grid">
        {/* LEFT: user select */}
        <div className="asgn-box">
          <div className="asgn-boxHead">
            <div className="asgn-boxTitle">Chọn User</div>
            <div className="asgn-pill">{loadingUsers ? "Loading..." : `${users.length} users`}</div>
          </div>

          <div className="asgn-field">
            <label className="asgn-label">Tìm nhanh</label>
            <input
              value={userQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setUserQuery(e.target.value)}
              placeholder="gõ username..."
              className="asgn-input"
            />
          </div>

          <div className="asgn-field">
            <label className="asgn-label">Danh sách</label>
            <select
              value={userId}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setUserId(e.target.value)}
              className="asgn-select"
              disabled={loadingUsers}
            >
              <option value="">-- chọn user --</option>
              {filteredUsers.map((u) => (
                <option key={u.userId} value={u.userId}>
                  {u.username}
                </option>
              ))}
            </select>
          </div>

          {selectedUser && (
            <div className="asgn-mini">
              <div className="asgn-miniRow">
                <span className="asgn-miniK">UserId</span>
                <span className="asgn-miniV asgn-mono">{selectedUser.userId}</span>
                <button className="asgn-btn tiny ghost" onClick={() => copy(selectedUser.userId)}>
                  Copy
                </button>
              </div>
            </div>
          )}
        </div>

        {/* RIGHT: assign form */}
        <div className="asgn-box">
          <div className="asgn-boxHead">
            <div className="asgn-boxTitle">Gán thiết bị</div>
            <div className="asgn-pill">{rows.length} devices</div>
          </div>

          <div className="asgn-field">
            <label className="asgn-label">Device GUID</label>
            <input
              value={deviceGuid}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDeviceGuid(e.target.value)}
              placeholder="90319774496194524687"
              className="asgn-input asgn-mono"
            />
          </div>

          <div className="asgn-field">
            <label className="asgn-label">
              Device name <span className="asgn-mutedSmall">(tuỳ chọn)</span>
            </label>
            <input
              value={deviceName}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDeviceName(e.target.value)}
              placeholder="Kho lạnh A / Xe tải 01 ..."
              className="asgn-input"
            />
          </div>

          <div className="asgn-actions">
            <button className="asgn-btn primary" disabled={!canSubmit || submitting} onClick={onAssign}>
              {submitting ? "Đang gán..." : "➕ Gán thiết bị"}
            </button>

            <button
              className="asgn-btn ghost"
              disabled={!userId || submitting}
              onClick={() => {
                setDeviceGuid("");
                setDeviceName("");
                setBanner("", "");
              }}
            >
              Làm mới form
            </button>
          </div>

          {!userId && <div className="asgn-hint">* Chọn user trước khi gán.</div>}
          {userId && <div className="asgn-hint">* User chỉ xem được các device đã gán.</div>}
        </div>
      </div>

      {/* TABLE */}
      <div className="asgn-tableWrap">
        <div className="asgn-tableTop">
          <div className="asgn-boxTitle">Thiết bị đã gán</div>
          <div className="asgn-tableRight">
            {loadingRows ? <span className="asgn-chip">Đang tải...</span> : <span className="asgn-chip">{rows.length} thiết bị</span>}
          </div>
        </div>

        <div className="asgn-tableShell">
          <table className="asgn-table">
            <thead>
              <tr>
                <th>DeviceGuid</th>
                <th>DeviceName</th>
                <th style={{ width: 140 }}></th>
              </tr>
            </thead>
            <tbody>
              {loadingRows && (
                <tr>
                  <td colSpan={3} className="asgn-empty">
                    Đang tải dữ liệu...
                  </td>
                </tr>
              )}

              {!loadingRows && rowsSorted.length === 0 && (
                <tr>
                  <td colSpan={3} className="asgn-empty">
                    Chưa có thiết bị.
                  </td>
                </tr>
              )}

              {!loadingRows &&
                rowsSorted.map((r) => (
                  <tr key={r.deviceGuid}>
                    <td className="asgn-mono">
                      <div className="asgn-cellGuid">
                        <span>{r.deviceGuid}</span>
                        <button className="asgn-btn tiny ghost" onClick={() => copy(r.deviceGuid)}>
                          Copy
                        </button>
                      </div>
                    </td>
                    <td>{r.deviceName || <span className="asgn-mutedSmall">—</span>}</td>
                    <td style={{ textAlign: "right" }}>
                      <button className="asgn-btn danger" disabled={!userId || submitting} onClick={() => onUnassign(r.deviceGuid)}>
                        ❌ Gỡ
                      </button>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        <div className="asgn-footnote">
          Tip: bạn có thể bọc API unassign bằng quyền Admin, và log lại ai gỡ/gán để audit.
        </div>
      </div>
    </div>
  );
}
