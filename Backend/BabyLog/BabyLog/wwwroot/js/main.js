document.addEventListener('DOMContentLoaded', function() {
    // 文件上传功能
    const fileInput = document.getElementById('file-upload');
    const dropzone = document.getElementById('dropzone');
    const uploadProgress = document.getElementById('upload-progress');
    const progressBar = document.getElementById('progress-bar');
    const progressText = document.getElementById('progress-text');
    const recordsContainer = document.getElementById('records-container');
    
    // 拖放功能
    dropzone.addEventListener('dragover', function(e) {
        e.preventDefault();
        dropzone.classList.add('active');
    });
    
    dropzone.addEventListener('dragleave', function() {
        dropzone.classList.remove('active');
    });
    
    dropzone.addEventListener('drop', function(e) {
        e.preventDefault();
        dropzone.classList.remove('active');
        
        if (e.dataTransfer.files.length) {
            fileInput.files = e.dataTransfer.files;
            handleFileUpload(e.dataTransfer.files);
        }
    });
    
    // 点击选择文件
    dropzone.addEventListener('click', function() {
        fileInput.click();
    });
    
    fileInput.addEventListener('change', function() {
        if (fileInput.files.length) {
            handleFileUpload(fileInput.files);
        }
    });
    
    // 处理文件上传
    function handleFileUpload(files) {
        // 显示进度条
        uploadProgress.style.display = 'block';
        
        const formData = new FormData();
        
        // 添加所有文件到表单数据
        for (let i = 0; i < files.length; i++) {
            formData.append('files', files[i]);
        }
        
        // 使用Fetch API上传文件
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/api/files/upload-multiple', true);
        
        // 进度事件
        xhr.upload.addEventListener('progress', function(e) {
            if (e.lengthComputable) {
                const percentComplete = Math.round((e.loaded / e.total) * 100);
                progressBar.style.width = percentComplete + '%';
                progressText.textContent = percentComplete + '%';
            }
        });
        
        // 加载完成事件
        xhr.addEventListener('load', function() {
            if (xhr.status === 200) {
                const response = JSON.parse(xhr.responseText);
                
                // 显示成功消息
                alert(response.message || '文件上传成功！');
                
                // 重置上传表单
                fileInput.value = '';
                uploadProgress.style.display = 'none';
                progressBar.style.width = '0%';
                progressText.textContent = '0%';
                
                // 加载新上传的文件
                loadFiles();
            } else {
                alert('上传失败：' + xhr.statusText);
            }
        });
        
        // 错误事件
        xhr.addEventListener('error', function() {
            alert('上传过程中发生错误');
        });
        
        // 发送请求
        xhr.send(formData);
    }
    
    // 加载文件列表
    function loadFiles() {
        fetch('/api/files/list')
            .then(response => response.json())
            .then(data => {
                if (data.success && data.data && data.data.length > 0) {
                    // 清除"没有记录"的信息
                    recordsContainer.innerHTML = '';
                    
                    // 按创建时间降序排序
                    const files = data.data.sort((a, b) => {
                        return new Date(b.creationTime) - new Date(a.creationTime);
                    });
                    
                    // 只显示最近10个文件
                    const recentFiles = files.slice(0, 10);
                    
                    // 创建文件卡片
                    recentFiles.forEach(file => {
                        const isImage = /\.(jpg|jpeg|png|gif|webp)$/i.test(file.fileName);
                        const isVideo = /\.(mp4|mov)$/i.test(file.fileName);
                        
                        const card = document.createElement('div');
                        card.className = 'record-card';
                        
                        // 添加预览（如果是图片）
                        if (isImage) {
                            card.innerHTML = `
                                <div class="record-preview">
                                    <img src="/api/files/download?fileName=${encodeURIComponent(file.fileName)}" alt="${file.fileName}">
                                </div>
                                <div class="record-info">
                                    <p class="record-name">${file.fileName}</p>
                                    <p class="record-date">${new Date(file.creationTime).toLocaleDateString()}</p>
                                </div>
                            `;
                        } else if (isVideo) {
                            card.innerHTML = `
                                <div class="record-preview">
                                    <video controls>
                                        <source src="/api/files/download?fileName=${encodeURIComponent(file.fileName)}" type="video/${file.fileName.split('.').pop()}">
                                        您的浏览器不支持视频标签。
                                    </video>
                                </div>
                                <div class="record-info">
                                    <p class="record-name">${file.fileName}</p>
                                    <p class="record-date">${new Date(file.creationTime).toLocaleDateString()}</p>
                                </div>
                            `;
                        } else {
                            card.innerHTML = `
                                <div class="record-preview file-icon">
                                    📄
                                </div>
                                <div class="record-info">
                                    <p class="record-name">${file.fileName}</p>
                                    <p class="record-date">${new Date(file.creationTime).toLocaleDateString()}</p>
                                </div>
                            `;
                        }
                        
                        recordsContainer.appendChild(card);
                    });
                    
                    // 添加记录卡片的样式
                    const style = document.createElement('style');
                    style.textContent = `
                        .record-card {
                            background-color: var(--card-background);
                            border-radius: 8px;
                            overflow: hidden;
                            box-shadow: 0 2px 6px var(--shadow-color);
                            transition: transform 0.3s ease;
                        }
                        
                        .record-card:hover {
                            transform: translateY(-5px);
                        }
                        
                        .record-preview {
                            height: 180px;
                            overflow: hidden;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            background-color: #f0f0f0;
                        }
                        
                        .record-preview img, .record-preview video {
                            width: 100%;
                            height: 100%;
                            object-fit: cover;
                        }
                        
                        .file-icon {
                            font-size: 48px;
                            color: var(--primary-color);
                        }
                        
                        .record-info {
                            padding: 15px;
                        }
                        
                        .record-name {
                            font-weight: 500;
                            margin-bottom: 5px;
                            white-space: nowrap;
                            overflow: hidden;
                            text-overflow: ellipsis;
                        }
                        
                        .record-date {
                            font-size: 14px;
                            color: #777;
                        }
                    `;
                    
                    document.head.appendChild(style);
                } else {
                    recordsContainer.innerHTML = '<div class="record-empty">没有找到记录。请先上传文件。</div>';
                }
            })
            .catch(error => {
                console.error('加载文件列表失败:', error);
                recordsContainer.innerHTML = '<div class="record-empty">加载文件列表失败。请稍后重试。</div>';
            });
    }
    
    // 初始加载文件列表
    loadFiles();
    
    // 平滑滚动到锚点
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            const targetElement = document.querySelector(targetId);
            
            if (targetElement) {
                const headerHeight = document.querySelector('header').offsetHeight;
                const targetPosition = targetElement.getBoundingClientRect().top + window.pageYOffset - headerHeight;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
});