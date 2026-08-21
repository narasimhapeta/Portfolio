import { useQuery } from '@tanstack/react-query'

async function fetchHealth(): Promise<string> {
  const response = await fetch(`${import.meta.env.VITE_API_BASE_URL}/health`)
  if (!response.ok) {
    throw new Error(`Health check failed: ${response.status}`)
  }
  return response.text()
}

export function CustomerOperationsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['health'],
    queryFn: fetchHealth,
  })

  const status = isLoading ? 'Checking...' : isError ? 'Unreachable' : data

  return (
    <main>
      <h1>Customer Operations</h1>
      <p>{`API Status: ${status}`}</p>
    </main>
  )
}
