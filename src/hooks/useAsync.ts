import { useEffect, useEffectEvent, useState } from 'react';
import { errorMessage } from '../services/api';

interface AsyncState<T> {
  data: T | null;
  error: string | null;
  loading: boolean;
}

/**
 * Loads data for `key` when the component mounts, whenever `key` changes, and on reload().
 * A response that arrives after the key changed or the component unmounted is dropped.
 * `data` keeps the previous value while reloading, so refreshes do not blank the screen.
 */
export function useAsync<K, T>(load: (key: K) => Promise<T>, key: K, fallbackError: string) {
  const [state, setState] = useState<AsyncState<T>>({ data: null, error: null, loading: true });
  const [request, setRequest] = useState({ key, attempt: 0 });

  // Adjusting state during render when an input changes, as React recommends instead of an effect.
  if (!Object.is(request.key, key)) {
    setRequest({ key, attempt: 0 });
    setState((current) => ({ ...current, error: null, loading: true }));
  }

  const fetchData = useEffectEvent((requestKey: K) => load(requestKey));
  const describeError = useEffectEvent((cause: unknown) => errorMessage(cause, fallbackError));

  useEffect(() => {
    let current = true;
    fetchData(request.key).then(
      (data) => {
        if (current) setState({ data, error: null, loading: false });
      },
      (cause: unknown) => {
        if (current) setState((previous) => ({ ...previous, error: describeError(cause), loading: false }));
      },
    );
    return () => {
      current = false;
    };
  }, [request]);

  const reload = () => {
    setState((current) => ({ ...current, error: null, loading: true }));
    setRequest((current) => ({ ...current, attempt: current.attempt + 1 }));
  };

  return { ...state, reload };
}
