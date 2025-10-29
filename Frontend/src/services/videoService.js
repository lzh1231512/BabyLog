// Video Player Service

import { getEventById, getVideoURL, getMediaUrl } from '@/api/events';
import { loadConfig , getBackendURL} from '@/config';
import logger from '@/utils/videoLogger';

/**
 * Service for video player operations
 */
export default class VideoService {
  /**
   * Load event and video data
   * @param {string} eventId - Event ID
   * @param {number} videoIndex - Video index
   * @returns {Promise<Object>} - Video data
   */
  static async loadVideoData(eventId, videoIndex = 0) {
    logger.info('Loading video data');
    
    try {
      // Ensure config is loaded
      await loadConfig();
      logger.success('Config loaded successfully');
      
      // Get event details
      logger.info(`Getting event details for ID: ${eventId}`);
      const response = await getEventById(eventId);
      
      if (!response.success) {
        logger.error(`Failed to get event: ${response.message}`);
        return {
          success: false,
          error: response.message || 'Failed to get event'
        };
      }
      
      const event = response.data;
      const videoList = event.media?.videos || [];
      logger.success(`Found ${videoList.length} videos in event`);
      
      if (videoList.length === 0) {
        logger.warning('No videos found in event');
        return {
          success: false,
          error: 'No videos found in event'
        };
      }
      
      // Set current video
      const validIndex = Math.min(videoIndex, videoList.length - 1);
      const currentVideo = videoList[validIndex];
      logger.info(`Selected video: ${currentVideo.fileName} (index: ${validIndex})`);
      
      return {
        success: true,
        event,
        videoList,
        currentVideo,
        currentVideoIndex: validIndex
      };
    } catch (error) {
      logger.error(`Error loading video data: ${error.message}`);
      return {
        success: false,
        error: 'Failed to load video data'
      };
    }
  }

  /**
   * Get video URL for playback
   * @param {string} eventId - Event ID
   * @param {string} fileName - Video file name
   * @returns {Promise<Object>} - Video URL data
   */
  static async getVideoUrl(eventId, fileName) {
    logger.info(`Getting video URL for ${fileName}`);
    
    try {
      const response = await getVideoURL(eventId, fileName);
      
      if (!response.success) {
        logger.error(`Failed to get video URL: ${response.message}`);
        return {
          success: false,
          error: response.message || 'Failed to get video URL'
        };
      }
      
      const videoData = response.data;
      logger.info(`Video status: processing=${videoData.isProcessing}, transcoded=${videoData.isTranscoded}`);
      
      // Check if video is processing
      if (videoData.isProcessing) {
        logger.warning('Video is still processing');
        return {
          success: false,
          error: 'Video is still processing',
          isProcessing: true
        };
      }
      
      // Check if video is transcoded
      if (!videoData.isTranscoded) {
        logger.warning('Video is not transcoded, playback may be limited');
      }
      
      logger.success(`Video URL retrieved successfully`);
      return {
        success: true,
        videoUrl: await getBackendURL() + videoData.hlsUrl,
        isTranscoded: videoData.isTranscoded
      };
    } catch (error) {
      logger.error(`Error getting video URL: ${error.message}`);
      return {
        success: false,
        error: 'Failed to get video URL'
      };
    }
  }
  
  /**
   * Get thumbnail URL for video
   * @param {string} eventId - Event ID
   * @param {string} fileName - Video file name
   * @returns {string} - Thumbnail URL
   */
  static getThumbnailUrl(eventId, fileName) {
    return getMediaUrl(eventId, fileName, true);
  }
}
