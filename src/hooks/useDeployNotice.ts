import { useEffect, useRef, useState } from 'react';
import { systemApi } from '../services/api';

const POLL_MS = 5000;

export interface DeployNoticeState {
  pending: boolean;
  updating: boolean;
  secondsLeft: number;
  message: string;
}

const IDLE: DeployNoticeState = { pending: false, updating: false, secondsLeft: 0, message: '' };

/**
 * Polls the deploy notice while the page is visible (and always once the update has started),
 * counts down to the deadline every second, and reloads when the notice is gone after the update.
 */
export function useDeployNotice(): DeployNoticeState {
  const [state, setState] = useState<DeployNoticeState>(IDLE);
  const deadline = useRef(0);
  const updating = useRef(false);

  useEffect(() => {
    let polling = false;
    let disposed = false;

    const tick = () => {
      setState((current) => {
        if (!current.pending) return current;
        const secondsLeft = Math.max(0, Math.ceil((deadline.current - Date.now()) / 1000));
        if (secondsLeft === 0) updating.current = true;
        return { ...current, secondsLeft, updating: secondsLeft === 0 };
      });
    };

    const poll = async () => {
      if (polling || (!updating.current && document.visibilityState !== 'visible')) return;
      polling = true;
      try {
        const notice = await systemApi.deployNotice();
        if (disposed) return;
        if (!notice || !Number.isFinite(notice.secondsRemaining)) {
          if (updating.current) window.location.reload();
          else setState(IDLE);
          return;
        }
        deadline.current = Date.now() + notice.secondsRemaining * 1000;
        setState((current) => ({ ...current, pending: true, message: notice.message }));
        tick();
      } catch {
        // While the site is offline every request fails; keep waiting.
      } finally {
        polling = false;
      }
    };

    const onVisible = () => {
      if (document.visibilityState === 'visible') void poll();
    };

    void poll();
    const pollTimer = window.setInterval(() => void poll(), POLL_MS);
    const tickTimer = window.setInterval(tick, 1000);
    document.addEventListener('visibilitychange', onVisible);

    return () => {
      disposed = true;
      window.clearInterval(pollTimer);
      window.clearInterval(tickTimer);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);

  return state;
}
