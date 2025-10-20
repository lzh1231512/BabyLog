// Video Player Utilities

/**
 * Format seconds into MM:SS format
 * @param {number} seconds - Duration in seconds
 * @returns {string} - Formatted duration
 */
export function formatDuration(seconds) {
  if (!seconds || isNaN(seconds)) return '00:00';
  
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

/**
 * Initialize Video.js player
 * @param {HTMLElement} element - Video element
 * @param {Object} options - Video.js options
 * @param {Function} onReady - Callback when player is ready
 * @param {Function} onError - Callback for errors
 * @returns {Object} - Video.js player instance
 */
export function initializeVideoPlayer(videojs, element, options, onReady, onError) {
  if (!videojs || !element) {
    if (onError) onError('Missing required parameters');
    return null;
  }

  try {
    const player = videojs(element, {
      fluid: true,
      responsive: true,
      width: '100%',
      height: 'auto',
      playbackRates: [0.5, 1, 1.25, 1.5, 2],
      controls: true,
      preload: 'auto',
      playsinline: true,
      autoplay: false,
      language: 'zh-cn',
      ...options
    });

    player.ready(() => {
      if (onReady) onReady(player);
    });

    player.on('error', () => {
      const error = player.error();
      if (onError) onError(error);
    });

    return player;
  } catch (err) {
    if (onError) onError(err.message);
    return null;
  }
}
