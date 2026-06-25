const COLORS: Record<string, string> = {
  Active: 'bg-green-100 text-green-800',
  Bound: 'bg-blue-100 text-blue-800',
  Cancelled: 'bg-red-100 text-red-800',
  Expired: 'bg-gray-100 text-gray-600',
  Submitted: 'bg-yellow-100 text-yellow-800',
  UnderReview: 'bg-orange-100 text-orange-800',
  Approved: 'bg-green-100 text-green-800',
  Denied: 'bg-red-100 text-red-800',
  Closed: 'bg-gray-100 text-gray-600',
  Success: 'bg-green-100 text-green-800',
  Pending: 'bg-yellow-100 text-yellow-800',
  Failed: 'bg-red-100 text-red-800',
};

export default function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${COLORS[status] ?? 'bg-gray-100 text-gray-600'}`}>
      {status}
    </span>
  );
}
