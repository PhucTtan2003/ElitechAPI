import App from "../App";
import AdminAssignDevicesPanel from "../pages/AdminAssignDevicesPanel";

export default function AdminRoot() {
  return (
    <>
      <App who="Admin" />
      <div style={{ marginTop: 12 }}>
        <AdminAssignDevicesPanel />
      </div>
    </>
  );
}
