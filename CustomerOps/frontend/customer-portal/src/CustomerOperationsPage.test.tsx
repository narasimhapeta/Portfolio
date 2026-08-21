import { render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { CustomerOperationsPage } from './CustomerOperationsPage'

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
  )
}

describe('CustomerOperationsPage', () => {
  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        text: () => Promise.resolve('Healthy'),
      })
    )
  })

  it('shows the API health status once loaded', async () => {
    renderWithClient(<CustomerOperationsPage />)

    expect(screen.getByText('API Status: Checking...')).toBeInTheDocument()

    await waitFor(() =>
      expect(screen.getByText('API Status: Healthy')).toBeInTheDocument()
    )
  })
})
