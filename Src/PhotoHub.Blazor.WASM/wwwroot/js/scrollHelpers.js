window.scrollHelpers = {
    getScrollContainer: function() {
        // En Blazor con MudBlazor, el scroll suele estar en .mud-main-content o en html/body
        const mainContent = document.querySelector('.mud-main-content');
        if (mainContent && mainContent.scrollHeight > mainContent.clientHeight) {
            // Verificamos si realmente tiene el scroll activo
            const style = window.getComputedStyle(mainContent);
            if (style.overflowY !== 'hidden' && style.display !== 'none') {
                return mainContent;
            }
        }
        
        // Si no es .mud-main-content, probamos con el layout o finalmente window
        const layout = document.querySelector('.mud-layout');
        if (layout && layout.scrollHeight > layout.clientHeight) {
            return layout;
        }

        return window;
    },
    scrollToElement: function (id) {
        const element = document.getElementById(id);
        const scrollContainer = this.getScrollContainer();
        if (element) {
            const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 0;
            
            // Calculamos la posición relativa al contenedor
            let elementPosition;
            if (scrollContainer === window) {
                elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
            } else {
                elementPosition = element.offsetTop;
            }
            
            const offsetPosition = elementPosition - 10;

            if (scrollContainer === window) {
                window.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            } else {
                scrollContainer.scrollTo({
                    top: offsetPosition,
                    behavior: 'smooth'
                });
            }
        }
    },
    onWindowScroll: function (dotnetHelper, groupIds) {
        const scrollContainer = this.getScrollContainer();
        
        const handleScroll = () => {
            let activeId = "";
            
            for (const id of groupIds) {
                const element = document.getElementById(id);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    // Ajustar rect.top relativo a la parte superior de la ventana
                    if (rect.top <= 150) { 
                        activeId = id;
                    }
                }
            }
            
            // Calcular progreso total del scroll
            let winScroll, height;
            if (scrollContainer === window) {
                winScroll = window.pageYOffset || document.documentElement.scrollTop;
                height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
            } else {
                winScroll = scrollContainer.scrollTop;
                height = scrollContainer.scrollHeight - scrollContainer.clientHeight;
            }
            
            const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
            dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, scrolled);
        };

        if (scrollContainer === window) {
            window.addEventListener('scroll', handleScroll, { passive: true });
        } else {
            scrollContainer.addEventListener('scroll', handleScroll, { passive: true });
        }
        window._timelineScrollHandler = handleScroll;
    },
    initTimelineDrag: function (dotnetHelper, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        let isDragging = false;

        const handleDrag = (e) => {
            if (!isDragging) return;
            
            const scrollContainer = this.getScrollContainer();
            const rect = container.getBoundingClientRect();
            const clientY = e.clientY || (e.touches && e.touches[0].clientY);
            const y = clientY - rect.top;
            let percentage = y / rect.height;
            percentage = Math.max(0, Math.min(1, percentage));
            
            if (scrollContainer === window) {
                const scrollHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
                window.scrollTo(0, scrollHeight * percentage);
            } else {
                const scrollHeight = scrollContainer.scrollHeight - scrollContainer.clientHeight;
                scrollContainer.scrollTop = scrollHeight * percentage;
            }
        };

        const startDragging = (e) => {
            isDragging = true;
            handleDrag(e);
            document.addEventListener('mousemove', handleDrag);
            document.addEventListener('mouseup', stopDragging);
            document.addEventListener('touchmove', handleDrag);
            document.addEventListener('touchend', stopDragging);
            container.classList.add('dragging');
        };

        const stopDragging = () => {
            isDragging = false;
            document.removeEventListener('mousemove', handleDrag);
            document.removeEventListener('mouseup', stopDragging);
            document.removeEventListener('touchmove', handleDrag);
            document.removeEventListener('touchend', stopDragging);
            container.classList.remove('dragging');
        };

        container.addEventListener('mousedown', startDragging);
        container.addEventListener('touchstart', startDragging);
    }
};

window.videoHelpers = {
    play: function (videoElement) {
        if (videoElement) {
            videoElement.play().catch(error => {
                // Ignore autoplay errors if any
                console.log("Autoplay prevented or video error: ", error);
            });
        }
    },
    pause: function (videoElement) {
        if (videoElement) {
            videoElement.pause();
            // Reset to beginning to show the first frame again
            videoElement.currentTime = 0;
        }
    }
};