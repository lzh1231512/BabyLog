// 事件相关的API接口
// 这里暂时使用静态数据模拟API调用

// 模拟数据
const mockEvents = [
  {
    id: 1,
    title: '初次见面',
    description: '宝宝来到这个世界的第一天，体重3.2kg，身长50cm。医生说宝宝很健康，哭声洪亮。第一次抱着宝宝的感觉真的很奇妙，那种责任感和幸福感交织在一起。',
    media: {
      images: [
        { fileName: 'birth_moment_1.jpg', desc: '宝宝刚出生的第一张照片' },
        { fileName: 'birth_weight.jpg', desc: '称重时的照片，3.2kg' },
        { fileName: 'first_family_photo.jpg', desc: '第一张全家福' }
      ],
      videos: [
        { fileName: 'first_cry.mp4', desc: '宝宝第一声啼哭' }
      ],
      audios: [
        { fileName: 'first_heartbeat.mp3', desc: '第一次听到的心跳声' },
        { fileName: 'birth_celebration.mp3', desc: '全家人的庆祝声音' }
      ]
    },
    date: '2025-05-09',
    location: '妇幼保健院'
  },
  {
    id: 2,
    title: '第一次微笑',
    description: '宝宝第一次对着爸爸妈妈微笑，那一刻感觉所有的疲惫都消失了。',
    media: {
      images: [
        { fileName: 'first_smile_1.jpg', desc: '对着妈妈微笑' },
        { fileName: 'first_smile_2.jpg', desc: '对着爸爸微笑' }
      ],
      videos: [
        { fileName: 'smile_moment.mp4', desc: '微笑的瞬间视频' }
      ],
      audios: []
    },
    date: '2025-05-15',
    location: '家里'
  },
  {
    id: 3,
    title: '满月照',
    description: '宝宝满月啦！拍了好多可爱的照片，每一张都舍不得删。',
    media: {
      images: [
        { fileName: 'month_photo_1.jpg', desc: '穿着小礼服的满月照' },
        { fileName: 'month_photo_2.jpg', desc: '和爸爸妈妈的合照' },
        { fileName: 'month_photo_3.jpg', desc: '可爱的睡姿' },
        { fileName: 'month_photo_4.jpg', desc: '第一套小衣服' },
        { fileName: 'month_photo_5.jpg', desc: '手脚印纪念' }
      ],
      videos: [
        { fileName: 'month_celebration.mp4', desc: '满月庆祝视频' }
      ],
      audios: [
        { fileName: 'month_blessing.mp3', desc: '亲友们的祝福话语' }
      ]
    },
    date: '2025-06-09',
    location: '摄影工作室'
  },
  {
    id: 4,
    title: '会翻身了',
    description: '宝宝学会了从仰卧翻到俯卧，好厉害！',
    media: {
      images: [
        { fileName: 'rollover_1.jpg', desc: '准备翻身的瞬间' },
        { fileName: 'rollover_2.jpg', desc: '成功翻身后的表情' }
      ],
      videos: [
        { fileName: 'rollover_process.mp4', desc: '翻身的完整过程' },
        { fileName: 'rollover_celebration.mp4', desc: '爸爸妈妈的庆祝' }
      ],
      audios: [
        { fileName: 'rollover_cheer.mp3', desc: '翻身成功时的欢呼声' }
      ]
    },
    date: '2025-08-15',
    location: '家里'
  },
  {
    id: 5,
    title: '第一次游泳',
    description: '带宝宝去婴儿游泳馆，第一次下水有点紧张',
    media: {
      images: [
        { fileName: 'swimming_1.jpg', desc: '准备下水的紧张表情' },
        { fileName: 'swimming_2.jpg', desc: '在水中的可爱模样' },
        { fileName: 'swimming_3.jpg', desc: '游泳后的满足表情' }
      ],
      videos: [
        { fileName: 'first_swim.mp4', desc: '第一次下水的视频' }
      ],
      audios: []
    },
    date: '2025-08-25',
    location: '婴儿游泳馆'
  },
  {
    id: 6,
    title: '开始吃辅食',
    description: '第一次尝试米糊，表情很有趣',
    media: {
      images: [
        { fileName: 'first_food_1.jpg', desc: '第一口米糊的表情' },
        { fileName: 'first_food_2.jpg', desc: '吃得满脸都是' },
        { fileName: 'first_food_3.jpg', desc: '似乎很喜欢的样子' },
        { fileName: 'first_food_4.jpg', desc: '用手抓食物探索' }
      ],
      videos: [
        { fileName: 'first_feeding.mp4', desc: '第一次喂辅食的过程' }
      ],
      audios: [
        { fileName: 'eating_sounds.mp3', desc: '吃东西时的可爱声音' }
      ]
    },
    date: '2025-10-01',
    location: '家里'
  },
  {
    id: 7,
    title: '会坐着了',
    description: '宝宝可以独自坐着玩玩具了，平衡感越来越好',
    media: {
      images: [
        { fileName: 'sitting_1.jpg', desc: '第一次独立坐着' },
        { fileName: 'sitting_2.jpg', desc: '坐着玩玩具' },
        { fileName: 'sitting_3.jpg', desc: '平衡感很好的表现' }
      ],
      videos: [
        { fileName: 'sitting_practice.mp4', desc: '练习坐着的过程' }
      ],
      audios: [
        { fileName: 'sitting_giggle.mp3', desc: '坐着时的开心笑声' }
      ]
    },
    date: '2025-11-15',
    location: '家里'
  },
  {
    id: 8,
    title: '会爬行了',
    description: '宝宝终于学会爬行，到处探索新世界',
    media: {
      images: [
        { fileName: 'crawling_1.jpg', desc: '第一次爬行的姿势' },
        { fileName: 'crawling_2.jpg', desc: '爬向玩具的瞬间' }
      ],
      videos: [
        { fileName: 'first_crawl.mp4', desc: '第一次爬行的完整视频' },
        { fileName: 'crawling_adventure.mp4', desc: '四处探索的爬行记录' }
      ],
      audios: []
    },
    date: '2026-02-10',
    location: '家里'
  },
  {
    id: 9,
    title: '第一颗牙齿',
    description: '小牙齿冒出来啦！开始长牙齿了',
    media: {
      images: [
        { fileName: 'first_tooth_1.jpg', desc: '第一颗小牙齿' },
        { fileName: 'first_tooth_2.jpg', desc: '张嘴露出小牙' },
        { fileName: 'first_tooth_3.jpg', desc: '咬牙胶的样子' }
      ],
      videos: [
        { fileName: 'tooth_discovery.mp4', desc: '发现长牙时的反应' }
      ],
      audios: [
        { fileName: 'teething_sounds.mp3', desc: '长牙时的咿呀声' }
      ]
    },
    date: '2026-02-20',
    location: '家里'
  },
  {
    id: 10,
    title: '生日派对',
    description: '宝宝的第一个生日！邀请了好多亲朋好友一起庆祝',
    media: {
      images: [
        { fileName: 'birthday_cake.jpg', desc: '第一个生日蛋糕' },
        { fileName: 'birthday_family.jpg', desc: '和家人的合影' },
        { fileName: 'birthday_gifts.jpg', desc: '收到的生日礼物' },
        { fileName: 'birthday_party.jpg', desc: '生日派对现场' },
        { fileName: 'birthday_candle.jpg', desc: '吹生日蜡烛' }
      ],
      videos: [
        { fileName: 'birthday_celebration.mp4', desc: '生日庆祝全过程' },
        { fileName: 'cake_smash.mp4', desc: '抓蛋糕的可爱瞬间' }
      ],
      audios: [
        { fileName: 'birthday_song.mp3', desc: '大家一起唱生日歌' },
        { fileName: 'birthday_wishes.mp3', desc: '亲友们的生日祝福' }
      ]
    },
    date: '2026-05-09',
    location: '家里'
  },
  {
    id: 11,
    title: '第一步',
    description: '宝宝迈出了人生的第一步，太激动了！',
    media: {
      images: [
        { fileName: 'first_step_1.jpg', desc: '准备迈步的瞬间' },
        { fileName: 'first_step_2.jpg', desc: '成功走出第一步' }
      ],
      videos: [
        { fileName: 'first_step.mp4', desc: '珍贵的第一步视频' },
        { fileName: 'walking_practice.mp4', desc: '后续的走路练习' }
      ],
      audios: [
        { fileName: 'first_step_cheer.mp3', desc: '走出第一步时的欢呼' }
      ]
    },
    date: '2026-05-20',
    location: '家里'
  },
  {
    id: 12,
    title: '会说话了',
    description: '宝宝开始说简单的词汇，"爸爸"、"妈妈"说得特别清楚',
    media: {
      images: [
        { fileName: 'first_words_1.jpg', desc: '说话时的认真表情' },
        { fileName: 'first_words_2.jpg', desc: '和爸爸妈妈对话' }
      ],
      videos: [
        { fileName: 'first_mama.mp4', desc: '第一次清楚说"妈妈"' },
        { fileName: 'first_baba.mp4', desc: '第一次清楚说"爸爸"' }
      ],
      audios: [
        { fileName: 'first_words.mp3', desc: '最初的词汇记录' },
        { fileName: 'babbling.mp3', desc: '可爱的咿呀学语' }
      ]
    },
    date: '2026-08-15',
    location: '家里'
  },
  {
    id: 13,
    title: '第一次上幼儿园',
    description: '开始尝试去托班，虽然哭了但很快就适应了',
    media: {
      images: [
        { fileName: 'first_daycare_1.jpg', desc: '准备去幼儿园' },
        { fileName: 'first_daycare_2.jpg', desc: '和老师的合影' },
        { fileName: 'first_daycare_3.jpg', desc: '和小朋友们玩耍' }
      ],
      videos: [
        { fileName: 'daycare_adaptation.mp4', desc: '逐渐适应幼儿园生活' }
      ],
      audios: []
    },
    date: '2026-11-10',
    location: '小星星幼儿园'
  },
  {
    id: 14,
    title: '学会用勺子',
    description: '可以自己用勺子吃饭了，虽然还会撒得到处都是',
    media: {
      images: [
        { fileName: 'spoon_learning_1.jpg', desc: '第一次握勺子' },
        { fileName: 'spoon_learning_2.jpg', desc: '自己舀饭吃' }
      ],
      videos: [
        { fileName: 'spoon_practice.mp4', desc: '学习用勺子的过程' }
      ],
      audios: [
        { fileName: 'eating_independence.mp3', desc: '独立吃饭时的开心声音' }
      ]
    },
    date: '2026-11-25',
    location: '家里'
  },
  {
    id: 15,
    title: '两岁生日',
    description: '宝宝两岁啦！性格越来越活泼可爱',
    media: {
      images: [
        { fileName: 'second_birthday_1.jpg', desc: '两岁生日的开心笑容' },
        { fileName: 'second_birthday_2.jpg', desc: '和小朋友们一起庆祝' },
        { fileName: 'second_birthday_3.jpg', desc: '收到心爱的玩具' },
        { fileName: 'second_birthday_4.jpg', desc: '自己吹蜡烛' }
      ],
      videos: [
        { fileName: 'second_birthday_party.mp4', desc: '两岁生日派对精彩时刻' }
      ],
      audios: [
        { fileName: 'second_birthday_song.mp3', desc: '两岁生日歌' }
      ]
    },
    date: '2027-05-09',
    location: '家里'
  },
  {
    id: 16,
    title: '学会骑平衡车',
    description: '开始学骑小车车，平衡感很好！',
    media: {
      images: [
        { fileName: 'balance_bike_1.jpg', desc: '第一次坐上平衡车' },
        { fileName: 'balance_bike_2.jpg', desc: '努力保持平衡' },
        { fileName: 'balance_bike_3.jpg', desc: '成功滑行的瞬间' }
      ],
      videos: [
        { fileName: 'balance_bike_learning.mp4', desc: '学习骑平衡车的过程' },
        { fileName: 'balance_bike_success.mp4', desc: '成功骑行的快乐时刻' }
      ],
      audios: []
    },
    date: '2027-05-20',
    location: '公园'
  },
  {
    id: 17,
    title: '会数数了',
    description: '可以从1数到10，还会背简单的儿歌',
    media: {
      images: [
        { fileName: 'counting_1.jpg', desc: '用手指数数' },
        { fileName: 'counting_2.jpg', desc: '认真数玩具的表情' }
      ],
      videos: [
        { fileName: 'counting_to_ten.mp4', desc: '从1数到10的视频' },
        { fileName: 'singing_nursery_rhyme.mp4', desc: '背诵儿歌' }
      ],
      audios: [
        { fileName: 'counting_practice.mp3', desc: '数数练习录音' },
        { fileName: 'nursery_rhymes.mp3', desc: '最喜欢的儿歌' }
      ]
    },
    date: '2027-11-15',
    location: '家里'
  },
  {
    id: 18,
    title: '第一次看电影',
    description: '带宝宝去电影院看动画片，全程都很安静',
    media: {
      images: [
        { fileName: 'first_movie_1.jpg', desc: '在电影院门口的合影' },
        { fileName: 'first_movie_2.jpg', desc: '专注看电影的侧影' },
        { fileName: 'first_movie_3.jpg', desc: '看完电影后的开心表情' }
      ],
      videos: [
        { fileName: 'movie_experience.mp4', desc: '第一次电影院体验' }
      ],
      audios: []
    },
    date: '2027-11-30',
    location: '万达影城'
  }
]

// 模拟API延迟
const delay = (ms) => new Promise(resolve => setTimeout(resolve, ms))

// 组织数据为时间线格式
const organizeDataForTimeline = (events) => {
  const grouped = {}
  
  events.forEach(event => {
    const eventDate = new Date(event.date)
    const birthDate = new Date('2025-05-09')
    
    // 计算年龄
    const totalMonths = (eventDate.getFullYear() - birthDate.getFullYear()) * 12 + 
                       (eventDate.getMonth() - birthDate.getMonth())
    
    let ageKey, ageLabel, dateLabel
    
    if (totalMonths === 0) {
      ageKey = '0-birth'
      ageLabel = '出生时'
      dateLabel = '2025年5月9日'
    } else if (totalMonths < 12) {
      ageKey = `${totalMonths}-months`
      ageLabel = `${totalMonths}月龄`
      dateLabel = eventDate.toLocaleDateString('zh-CN', { year: 'numeric', month: 'long' })
    } else {
      const years = Math.floor(totalMonths / 12)
      const remainingMonths = totalMonths % 12
      if (remainingMonths === 0) {
        ageKey = `${years}-years`
        ageLabel = `${years}周岁`
      } else {
        ageKey = `${years}-${remainingMonths}-years-months`
        ageLabel = `${years}岁${remainingMonths}月`
      }
      dateLabel = eventDate.toLocaleDateString('zh-CN', { year: 'numeric', month: 'long' })
    }
    
    if (!grouped[ageKey]) {
      grouped[ageKey] = {
        age: ageLabel,
        date: dateLabel,
        events: []
      }
    }
    
    grouped[ageKey].events.push(event)
  })
  
  // 按时间顺序排序
  return Object.values(grouped).sort((a, b) => {
    if (a.events[0] && b.events[0]) {
      return new Date(a.events[0].date) - new Date(b.events[0].date)
    }
    return 0
  })
}

// API接口函数

/**
 * 获取所有事件列表（时间线格式）
 */
export const getEventsList = async () => {
  try {
    await delay(300) // 模拟网络延迟
    const timelineData = organizeDataForTimeline(mockEvents)
    return {
      success: true,
      data: timelineData,
      message: '获取事件列表成功'
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '获取事件列表失败'
    }
  }
}

/**
 * 根据ID获取单个事件详情
 */
export const getEventById = async (id) => {
  try {
    await delay(200) // 模拟网络延迟
    const event = mockEvents.find(e => e.id === parseInt(id))
    
    if (event) {
      return {
        success: true,
        data: event,
        message: '获取事件详情成功'
      }
    } else {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '获取事件详情失败'
    }
  }
}

/**
 * 创建新事件
 */
export const createEvent = async (eventData) => {
  try {
    await delay(500) // 模拟网络延迟
    const newEvent = {
      id: Math.max(...mockEvents.map(e => e.id)) + 1,
      ...eventData,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
    
    mockEvents.push(newEvent)
    
    return {
      success: true,
      data: newEvent,
      message: '创建事件成功'
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '创建事件失败'
    }
  }
}

/**
 * 更新事件
 */
export const updateEvent = async (id, eventData) => {
  try {
    await delay(500) // 模拟网络延迟
    const index = mockEvents.findIndex(e => e.id === parseInt(id))
    
    if (index !== -1) {
      mockEvents[index] = {
        ...mockEvents[index],
        ...eventData,
        updatedAt: new Date().toISOString()
      }
      
      return {
        success: true,
        data: mockEvents[index],
        message: '更新事件成功'
      }
    } else {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '更新事件失败'
    }
  }
}

/**
 * 删除事件
 */
export const deleteEvent = async (id) => {
  try {
    await delay(300) // 模拟网络延迟
    const index = mockEvents.findIndex(e => e.id === parseInt(id))
    
    if (index !== -1) {
      const deletedEvent = mockEvents.splice(index, 1)[0]
      return {
        success: true,
        data: deletedEvent,
        message: '删除事件成功'
      }
    } else {
      return {
        success: false,
        data: null,
        message: '事件不存在'
      }
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '删除事件失败'
    }
  }
}

/**
 * 上传单个文件
 */
export const uploadFile = async (file, type = 'image') => {
  try {
    await delay(800) // 模拟文件上传时间
    
    // 生成服务器端文件名（模拟）
    const timestamp = Date.now()
    const randomStr = Math.random().toString(36).substring(2, 8)
    const extension = file.name.split('.').pop()
    const serverFileName = `${type}_${timestamp}_${randomStr}.${extension}`
    
    return {
      success: true,
      data: {
        originalName: file.name,
        serverFileName: serverFileName,
        size: file.size,
        type: file.type,
        uploadTime: new Date().toISOString()
      },
      message: '文件上传成功'
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '文件上传失败'
    }
  }
}

/**
 * 批量上传文件
 */
export const uploadFiles = async (files, type = 'image') => {
  try {
    const uploadPromises = files.map(file => uploadFile(file, type))
    const results = await Promise.all(uploadPromises)
    
    const successResults = results.filter(result => result.success)
    const failedResults = results.filter(result => !result.success)
    
    return {
      success: failedResults.length === 0,
      data: {
        successful: successResults.map(result => result.data),
        failed: failedResults.length
      },
      message: failedResults.length === 0 
        ? `成功上传 ${successResults.length} 个文件`
        : `上传完成：成功 ${successResults.length} 个，失败 ${failedResults.length} 个`
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '批量上传失败'
    }
  }
}

/**
 * 获取统计数据
 */
export const getStats = async () => {
  try {
    await delay(100) // 模拟网络延迟
    
    const totalEvents = mockEvents.length
    const totalPhotos = mockEvents.reduce((total, event) => total + event.media.images.length, 0)
    const totalVideos = mockEvents.reduce((total, event) => total + event.media.videos.length, 0)
    const totalAudios = mockEvents.reduce((total, event) => total + event.media.audios.length, 0)
    
    return {
      success: true,
      data: {
        totalEvents,
        totalPhotos,
        totalVideos,
        totalAudios
      },
      message: '获取统计数据成功'
    }
  } catch (error) {
    return {
      success: false,
      data: null,
      message: '获取统计数据失败'
    }
  }
}
