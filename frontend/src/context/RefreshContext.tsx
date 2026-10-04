import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";

interface RefreshContextValue {
  version: number;
  refresh: () => void;
}

const RefreshContext = createContext<RefreshContextValue | null>(null);

export function RefreshProvider({ children }: { children: ReactNode }) {
  const [version, setVersion] = useState(0);

  const refresh = useCallback(() => setVersion((v) => v + 1), []);

  const value = useMemo(() => ({ version, refresh }), [version, refresh]);

  return (
    <RefreshContext.Provider value={value}>{children}</RefreshContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useRefresh() {
  const ctx = useContext(RefreshContext);
  if (!ctx) throw new Error("useRefresh must be used within RefreshProvider");
  return ctx;
}