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
        window.onscroll = () => {
            let activeId = "";
            const appBarHeight = document.querySelector('.mud-appbar')?.offsetHeight || 64;
            
            for (const id of groupIds) {
                const element = document.getElementById(id);
                if (element) {
                    const rect = element.getBoundingClientRect();
                    // Si la parte superior del elemento está cerca de la parte superior de la ventana (debajo del appBar)
                    if (rect.top <= appBarHeight + 20) {
                        activeId = id;
                    }
                }
            }
            
            if (activeId) {
                dotnetHelper.invokeMethodAsync('OnGroupVisible', activeId);
            }
        };
    }
};