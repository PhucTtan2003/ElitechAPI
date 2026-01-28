import React from 'react'

export default function Dashboard({ who }: { who: string }) {
  const [now, setNow] = React.useState<string>('')

  React.useEffect(() => {
    const id = setInterval(() => setNow(new Date().toLocaleString()), 1000)
    return () => clearInterval(id)
  }, [])

  return (
    <div className="border rounded p-3">
      <div className="mb-2">Bảng điều khiển: <strong>{who}</strong></div>
      <div className="text-muted">Thời gian: {now}</div>
      <p className="mt-3">
        Đây là sườn React. Bạn có thể thêm chart, table, SignalR… vào đây.
      </p>
    </div>
  )
}