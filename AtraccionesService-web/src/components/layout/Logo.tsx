import { Link } from 'react-router-dom';

export function Logo({ inverted = false }: { inverted?: boolean }) {
  return (
    <Link to="/" className="flex shrink-0 items-center gap-2" aria-label="TourGirls, inicio">
      <svg width="34" height="34" viewBox="0 0 32 32" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill="#C94D6B" />
        <path d="M5 24 13 11l5 7 3-4 6 10z" fill="#FFF9F7" />
        <circle cx="23" cy="9" r="3" fill="#F4B183" />
      </svg>
      <span className={`text-[19px] font-extrabold leading-none tracking-tight ${inverted ? 'text-white' : 'text-ink'}`}>
        Tour<span className="text-brand-500">Girls</span>
      </span>
    </Link>
  );
}
