window.scrollHelpers = {
    scrollToElement: function (id) {
        const element = document.getElementById(id);
        if (element) {
            const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 0;
            const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
            const offsetPosition = elementPosition - appBarHeight - 10;

            window.scrollTo({
                top: offsetPosition,
                behavior: 'smooth'
            });
        }
    },
    onWindowScroll: function (dotnetHelper, groupIds) {
        const handleScroll = () => {
            let activeId = "";
            const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 64;
            const threshold = appBarHeight + 100;
            
            for (const id of groupIds) {
                const element = document.getElementById(id);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    if (rect.top <= threshold) {
                        activeId = id;
                    }
                }
            }
            
            // Calcular progreso total del scroll
            const winScroll = window.pageYOffset || document.documentElement.scrollTop;
            const height = document.documentElement.scrollHeight - window.innerHeight;
            const scrolled = height > 0 ? (winScroll / height) * 100 : 0;
            
            dotnetHelper.invokeMethodAsync('OnScrollUpdated', activeId, scrolled);
        };

        window.addEventListener('scroll', handleScroll, { passive: true });
        window._timelineScrollHandler = handleScroll;
    },
    initTimelineDrag: function (dotnetHelper, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        let isDragging = false;

        const handleDrag = (e) => {
            if (!isDragging) return;
            
            const rect = container.getBoundingClientRect();
            const y = (e.clientY || (e.touches && e.touches[0].clientY)) - rect.top;
            let percentage = y / rect.height;
            percentage = Math.max(0, Math.min(1, percentage));
            
            const scrollHeight = document.documentElement.scrollHeight - window.innerHeight;
            window.scrollTo(0, scrollHeight * percentage);
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